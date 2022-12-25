using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Lampac.Engine;
using Lampac.Engine.CORE;

namespace Lampac.Controllers.Spankbang
{
    public class ViewController : BaseController
    {
        [HttpGet]
        [Route("sbg/vidosik")]
        async public Task<ActionResult> Index(string goni)
        {
            if (!AppInit.conf.Spankbang.enable)
                return OnError("disable");

            string memKey = $"Spankbang:vidosik:{goni}";
            if (!memoryCache.TryGetValue(memKey, out string json))
            {
                string html = await HttpClient.Get($"{AppInit.conf.Spankbang.host}/{goni}", timeoutSeconds: 10/*, useproxy: AppInit.conf.Spankbang.useproxy*/);
                if (html == null)
                    return OnError("html");

                #region Получаем ссылки на mp4
                string csrf_session = await getCsrfSession();
                string streamkey = new Regex("data-streamkey=\"([^\"]+)\"").Match(html).Groups[1].Value;
                json = await HttpClient.Post($"{AppInit.conf.Spankbang.host}/api/videos/stream", $"&id={streamkey}&data=0&sb_csrf_session={csrf_session}", timeoutSeconds: 8);
                if (json == null)
                    return OnError("json");
                #endregion

                memoryCache.Set(memKey, json, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            #region Достаем ссылки на поток
            var stream_links = new Dictionary<string, string>();

            var match = new Regex("\"([0-9]+)(p|k)\":\\[\"(https?://[^\"]+)\"").Match(json);
            while (match.Success)
            {
                string hls = match.Groups[3].Value.Replace("https:", "http:");
                stream_links.TryAdd($"{match.Groups[1].Value}{match.Groups[2].Value}", AppInit.HostStreamProxy(HttpContext, AppInit.conf.Spankbang.streamproxy, hls));
                match = match.NextMatch();
            }
            #endregion

            if (stream_links.Count == 0)
                return OnError("stream_links");

            stream_links = stream_links.OrderByDescending(i => i.Key == "4k").ThenByDescending(i => int.Parse(i.Key.Replace("p", "").Replace("k", ""))).ToDictionary(k => k.Key, v => v.Value);
            return Json(stream_links);
        }


        #region getCSRF
        async public ValueTask<string> getCsrfSession()
        {
            string memKey = "porn-spankbang:getCSRF-sb_csrf_session";
            if (memoryCache.TryGetValue(memKey, out string csrf))
                return csrf;

            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.MaxResponseContentBufferSize = 2000000; // 2MB
                    client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/75.0.3770.100 Safari/537.36");

                    using (var response = await client.GetAsync(AppInit.conf.Spankbang.host))
                    {
                        if (response.Headers.TryGetValues("Set-Cookie", out var cook))
                        {
                            foreach (string line in cook)
                            {
                                if (line.Contains("sb_csrf_session="))
                                {
                                    csrf = new Regex("sb_csrf_session=([^;]+)(;|$)").Match(line).Groups[1].Value;
                                    memoryCache.Set(memKey, csrf, TimeSpan.FromMinutes(10));
                                    return csrf;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return string.Empty;
        }
        #endregion
    }
}

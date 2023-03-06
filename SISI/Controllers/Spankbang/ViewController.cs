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
            if (!memoryCache.TryGetValue(memKey, out string stream_data))
            {
                string html = await HttpClient.Get($"{AppInit.conf.Spankbang.host}/{goni}", timeoutSeconds: 10, httpversion: 2, addHeaders: new List<(string name, string val)>()
                {
                    ("cache-control", "no-cache"),
                    ("dnt", "1"),
                    ("pragma", "no-cache"),
                    ("sec-ch-ua", "\"Chromium\";v=\"110\", \"Not A(Brand\";v=\"24\", \"Google Chrome\";v=\"110\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-fetch-dest", "document"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "none"),
                    ("sec-fetch-user", "?1"),
                    ("upgrade-insecure-requests", "1")
                });

                stream_data = StringConvert.FindLastText(html ?? "", "stream_data", "</script>");

                if (string.IsNullOrWhiteSpace(stream_data))
                    return OnError("stream_data");

                memoryCache.Set(memKey, stream_data, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            #region Достаем ссылки на поток
            var stream_links = new Dictionary<string, string>();

            var match = new Regex("'([0-9]+)(p|k)': ?\\[\'(https?://[^']+)\'").Match(stream_data);
            while (match.Success)
            {
                string hls = match.Groups[3].Value.Replace("https:", "http:");
                stream_links.TryAdd($"{match.Groups[1].Value}{match.Groups[2].Value}", HostStreamProxy(AppInit.conf.Spankbang.streamproxy, hls));
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

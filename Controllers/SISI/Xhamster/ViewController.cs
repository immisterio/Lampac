using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace Lampac.Controllers.Xhamster
{
    public class ViewController : BaseController
    {
        [HttpGet]
        [Route("xmr/vidosik.m3u8")]
        async public Task<ActionResult> Index(string goni)
        {
            if (!AppInit.conf.Xhamster.enable)
                return OnError("disable");

            string memKey = $"Xhamster:vidosik:{goni}";
            if (!memoryCache.TryGetValue(memKey, out (string hls, string stream_link) cache))
            {
                string html = await HttpClient.Get($"{AppInit.conf.Xhamster.host}/{goni}", useproxy: AppInit.conf.Xhamster.useproxy);
                if (html == null)
                    return OnError("html");

                string stream_link = new Regex("\"hls\":{\"url\":\"([^\"]+)\"").Match(html).Groups[1].Value.Replace("\\", "");
                if (string.IsNullOrWhiteSpace(stream_link))
                    return OnError("stream_link");

                if (stream_link.StartsWith("/"))
                    stream_link = AppInit.conf.Xhamster.host + stream_link;

                if (!stream_link.Contains(".m3u"))
                {
                    string m3u8 = await HttpClient.Get(stream_link, useproxy: AppInit.conf.Xhamster.useproxy, timeoutSeconds: 8);
                    if (m3u8 != null)
                    {
                        // Ссылка на сервер источника
                        string masterHost = new Regex("^(https?://[^/]+)").Match(stream_link).Groups[1].Value;

                        // Меняем ссылки которые начинаются с "/" на "http"
                        cache.hls = Regex.Replace(m3u8, "([\n\r\t ]+)/", $"$1{masterHost}/");
                    }
                }

                cache.stream_link = stream_link;
                memoryCache.Set(memKey, cache, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            if (cache.hls != null)
                return Content(cache.hls, "application/vnd.apple.mpegurl");

            return Redirect(cache.stream_link);
        }
    }
}

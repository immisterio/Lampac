using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace Lampac.Controllers.Xnxx
{
    public class ViewController : BaseController
    {
        [HttpGet]
        [Route("xnx/vidosik")]
        async public Task<ActionResult> Index(string goni)
        {
            if (!AppInit.conf.Xnxx.enable)
                return OnError("disable");

            string memKey = $"Xnxx:vidosik:{goni}";
            if (!memoryCache.TryGetValue(memKey, out (string m3u8, string stream_link) cache))
            {
                string html = await HttpClient.Get($"{AppInit.conf.Xnxx.host}/{Regex.Replace(goni, "^([^/]+)/.*", "$1/_")}", timeoutSeconds: 10, useproxy: AppInit.conf.Xnxx.useproxy);
                if (html == null)
                    return OnError("html");

                string stream_link = new Regex("html5player\\.setVideoHLS\\('([^']+)'\\);").Match(html).Groups[1].Value;
                if (string.IsNullOrWhiteSpace(stream_link))
                    return OnError("stream_link");

                string m3u8 = await HttpClient.Get(stream_link, timeoutSeconds: 8, useproxy: AppInit.conf.Xnxx.useproxy);
                if (m3u8 == null)
                    return OnError("m3u8");

                cache = (m3u8, stream_link);
                memoryCache.Set(memKey, cache, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 15 : 5));
            }

            var stream_links = new Dictionary<string, string>();
            foreach (string quality in new List<string>() { "2160", "1440", "1080", "720", "480", "360", "250" })
            {
                string hls = Regex.Match(cache.m3u8, $"(hls-{quality}p[^\n\r\t ]+)").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(hls))
                    continue;

                hls = $"{Regex.Replace(cache.stream_link, "/hls\\.m3u.*", "")}/{hls}".Replace("https:", "http:");
                stream_links.Add($"{quality}p", AppInit.HostStreamProxy(HttpContext, AppInit.conf.Xnxx.streamproxy, hls));
            }

            if (stream_links.Count == 0)
                return OnError("stream_links");

            return Json(stream_links);
        }
    }
}

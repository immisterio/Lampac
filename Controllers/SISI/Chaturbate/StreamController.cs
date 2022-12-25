using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Controllers.Chaturbate
{
    public class StreamController : BaseController
    {
        [HttpGet]
        [Route("chu/potok.m3u8")]
        async public Task<ActionResult> Index(string baba)
        {
            if (!AppInit.conf.Chaturbate.enable)
                return OnError("disable");

            string memKey = $"chaturbate:stream:{baba}";
            if (memoryCache.TryGetValue(memKey, out string hls))
                return Redirect(AppInit.HostStreamProxy(HttpContext, AppInit.conf.Chaturbate.streamproxy, hls));

            string html = await HttpClient.Get($"{AppInit.conf.Chaturbate.host}/{baba}/", useproxy: AppInit.conf.Chaturbate.useproxy);
            if (html == null)
                return OnError("html");

            hls = new Regex("(https?://[^ ]+/playlist\\.m3u8)").Match(html).Groups[1].Value;
            if (string.IsNullOrWhiteSpace(hls))
                return OnError("hls");

            hls = hls.Replace("\\u002D", "-").Replace("\\", "");
            memoryCache.Set(memKey, hls, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 10 : 5));

            return Redirect(AppInit.HostStreamProxy(HttpContext, AppInit.conf.Chaturbate.streamproxy, hls));
        }
    }
}

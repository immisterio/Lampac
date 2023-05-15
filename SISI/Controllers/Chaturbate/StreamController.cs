using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.CORE;
using Shared.Engine.SISI;

namespace Lampac.Controllers.Chaturbate
{
    public class StreamController : BaseController
    {
        [HttpGet]
        [Route("chu/potok")]
        async public Task<ActionResult> Index(string baba)
        {
            if (!AppInit.conf.Chaturbate.enable)
                return OnError("disable");

            string memKey = $"chaturbate:stream:{baba}";
            if (!memoryCache.TryGetValue(memKey, out Dictionary<string, string> stream_links))
            {
                var proxyManager = new ProxyManager("chu", AppInit.conf.Chaturbate);
                var proxy = proxyManager.Get();

                stream_links = await ChaturbateTo.StreamLinks(AppInit.conf.Chaturbate.corsHost(), baba, url => HttpClient.Get(url, timeoutSeconds: 10, proxy: proxy));
                if (stream_links == null || stream_links.Count == 0)
                {
                    proxyManager.Refresh();
                    return OnError("stream_links");
                }

                memoryCache.Set(memKey, stream_links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 10 : 5));
            }

            return Json(stream_links.ToDictionary(k => k.Key, v => HostStreamProxy(AppInit.conf.Chaturbate.streamproxy, v.Value)));
        }
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared.Engine.SISI;
using System.Collections.Generic;
using System.Linq;
using Shared.Engine.CORE;
using SISI;

namespace Lampac.Controllers.Xhamster
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("xmr/vidosik")]
        async public Task<ActionResult> Index(string uri)
        {
            if (!AppInit.conf.Xhamster.enable)
                return OnError("disable");

            var proxyManager = new ProxyManager("xmr", AppInit.conf.Xhamster);
            var proxy = proxyManager.Get();

            string memKey = $"xhamster:view:{uri}";
            if (!memoryCache.TryGetValue(memKey, out Dictionary<string, string> stream_links))
            {
                stream_links = await XhamsterTo.StreamLinks(AppInit.conf.Xhamster.host, uri, url => HttpClient.Get(url, timeoutSeconds: 10, proxy: proxy));

                if (stream_links == null || stream_links.Count == 0)
                    return OnError("stream_links", proxyManager);

                memoryCache.Set(memKey, stream_links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            return Json(stream_links.ToDictionary(k => k.Key, v => HostStreamProxy(AppInit.conf.Xhamster, v.Value, proxy: proxy)));
        }
    }
}

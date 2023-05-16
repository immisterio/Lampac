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

namespace Lampac.Controllers.Ebalovo
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("elo/vidosik")]
        async public Task<ActionResult> Index(string uri)
        {
            if (!AppInit.conf.Ebalovo.enable)
                return OnError("disable");

            var proxyManager = new ProxyManager("elo", AppInit.conf.Ebalovo);
            var proxy = proxyManager.Get();

            string memKey = $"ebalovo:view:{uri}";
            if (!memoryCache.TryGetValue(memKey, out Dictionary<string, string> stream_links))
            {
                stream_links = await EbalovoTo.StreamLinks(AppInit.conf.Ebalovo.host, uri,
                               url => HttpClient.Get(url, timeoutSeconds: 8, proxy: proxy),
                               location => HttpClient.GetLocation(location, timeoutSeconds: 8, proxy: proxy, referer: $"{AppInit.conf.Ebalovo.host}/"));

                if (stream_links == null || stream_links.Count == 0)
                    return OnError("stream_links", proxyManager);

                memoryCache.Set(memKey, stream_links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            return Json(stream_links.ToDictionary(k => k.Key, v => HostStreamProxy(AppInit.conf.Ebalovo, v.Value, proxy: proxy)));
        }
    }
}

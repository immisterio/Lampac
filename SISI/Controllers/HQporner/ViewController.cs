using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;

namespace Lampac.Controllers.HQporner
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("hqr/vidosik")]
        async public Task<ActionResult> Index(string uri)
        {
            if (!AppInit.conf.HQporner.enable)
                return OnError("disable");

            var proxyManager = new ProxyManager("hqr", AppInit.conf.HQporner);
            var proxy = proxyManager.Get();

            string memKey = $"HQporner:view:{uri}";
            if (!memoryCache.TryGetValue(memKey, out Dictionary<string, string> stream_links))
            {
                stream_links = await HQpornerTo.StreamLinks(AppInit.conf.HQporner.host, uri, 
                               htmlurl => HttpClient.Get(htmlurl, timeoutSeconds: 8, proxy: proxy), 
                               iframeurl => HttpClient.Get(iframeurl, timeoutSeconds: 8, proxy: proxy));

                if (stream_links == null || stream_links.Count == 0)
                    return OnError("stream_links", proxyManager);

                memoryCache.Set(memKey, stream_links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            return OnResult(stream_links, AppInit.conf.HQporner, proxy);
        }
    }
}

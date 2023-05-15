using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;
using System.Linq;
using Shared.Engine.CORE;

namespace Lampac.Controllers.Eporner
{
    public class ViewController : BaseController
    {
        [HttpGet]
        [Route("epr/vidosik")]
        async public Task<ActionResult> Index(string uri)
        {
            if (!AppInit.conf.Eporner.enable)
                return OnError("disable");

            string memKey = $"eporner:view:{uri}";
            if (!memoryCache.TryGetValue(memKey, out Dictionary<string, string> stream_links))
            {
                var proxyManager = new ProxyManager("epr", AppInit.conf.Eporner);
                var proxy = proxyManager.Get();

                stream_links = await EpornerTo.StreamLinks(AppInit.conf.Eporner.host, uri, 
                               htmlurl => HttpClient.Get(htmlurl, timeoutSeconds: 8, proxy: proxy), 
                               jsonurl => HttpClient.Get(jsonurl, timeoutSeconds: 8, proxy: proxy));

                if (stream_links == null || stream_links.Count == 0)
                {
                    proxyManager.Refresh();
                    return OnError("stream_links");
                }

                memoryCache.Set(memKey, stream_links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 2));
            }

            return Json(stream_links.ToDictionary(k => k.Key, v => HostStreamProxy(AppInit.conf.Eporner.streamproxy, v.Value)));
        }
    }
}

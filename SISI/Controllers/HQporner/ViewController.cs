using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;

namespace Lampac.Controllers.HQporner
{
    public class ViewController : BaseController
    {
        [HttpGet]
        [Route("hqr/vidosik")]
        async public Task<ActionResult> Index(string uri)
        {
            if (!AppInit.conf.HQporner.enable)
                return OnError("disable");

            string memKey = $"HQporner:view:{uri}";
            if (!memoryCache.TryGetValue(memKey, out Dictionary<string, string> stream_links))
            {
                System.Net.WebProxy proxy = null;
                if (AppInit.conf.HQporner.useproxy)
                    proxy = HttpClient.webProxy();

                stream_links = await HQpornerTo.StreamLinks(AppInit.conf.HQporner.host, uri, 
                               htmlurl => HttpClient.Get(htmlurl, timeoutSeconds: 8, proxy: proxy), 
                               iframeurl => HttpClient.Get(iframeurl, timeoutSeconds: 8, proxy: proxy));

                if (stream_links == null || stream_links.Count == 0)
                    return OnError("stream_links");

                memoryCache.Set(memKey, stream_links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            return Json(stream_links.ToDictionary(k => k.Key, v => HostStreamProxy(AppInit.conf.HQporner.streamproxy, v.Value)));
        }
    }
}

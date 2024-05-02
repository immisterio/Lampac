using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;

namespace Lampac.Controllers.HQporner
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("hqr/vidosik")]
        async public Task<JsonResult> Index(string uri)
        {
            var init = AppInit.conf.HQporner;

            if (!init.enable)
                return OnError("disable");

            var proxyManager = new ProxyManager("hqr", init);
            var proxy = proxyManager.Get();

            string memKey = $"HQporner:view:{uri}";
            if (!hybridCache.TryGetValue(memKey, out Dictionary<string, string> stream_links))
            {
                stream_links = await HQpornerTo.StreamLinks(init.corsHost(), uri, 
                               htmlurl => HttpClient.Get(init.cors(htmlurl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)), 
                               iframeurl => HttpClient.Get(init.cors(iframeurl), timeoutSeconds: 8, proxy: proxy, referer: init.host, headers: httpHeaders(init)));

                if (stream_links == null || stream_links.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, stream_links, cacheTime(20, init: init));
            }

            return OnResult(stream_links, init, proxy);
        }
    }
}

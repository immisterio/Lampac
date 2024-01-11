using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;

namespace Lampac.Controllers.Xhamster
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("xmr/vidosik")]
        async public Task<ActionResult> Index(string uri)
        {
            var init = AppInit.conf.Xhamster;

            if (!init.enable)
                return OnError("disable");

            var proxyManager = new ProxyManager("xmr", init);
            var proxy = proxyManager.Get();

            string memKey = $"xhamster:view:{uri}";
            if (!memoryCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await XhamsterTo.StreamLinks($"{host}/xmr/vidosik", init.corsHost(), uri, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                memoryCache.Set(memKey, stream_links, cacheTime(20));
            }

            return OnResult(stream_links, init, proxy, plugin: "xmr");
        }
    }
}

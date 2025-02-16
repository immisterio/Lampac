using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;

namespace Lampac.Controllers.Spankbang
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("sbg/vidosik")]
        async public Task<ActionResult> Index(string uri, bool related)
        {
            if (IsBadInitialization(AppInit.conf.Spankbang, out ActionResult action))
                return action;

            var proxyManager = new ProxyManager("sbg", init);
            var proxy = proxyManager.Get();

            string memKey = $"spankbang:view:{uri}";
            if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                stream_links = await SpankbangTo.StreamLinks($"{host}/sbg/vidosik", init.corsHost(), uri, url =>
                    rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : HttpClient.Get(init.cors(url), httpversion: 2, timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                );

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                {
                    if (IsRhubFallback(init))
                        goto reset;

                    return OnError("stream_links", proxyManager);
                }

                if (!rch.enable)
                    proxyManager.Success();

                hybridCache.Set(memKey, stream_links, cacheTime(20, init: init));
            }

            if (related)
                return OnResult(stream_links?.recomends, null, plugin: "sbg", total_pages: 1);

            return OnResult(stream_links, init, proxy, plugin: "sbg");
        }
    }
}

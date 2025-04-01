using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;

namespace Lampac.Controllers.Eporner
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("epr/vidosik")]
        async public Task<ActionResult> Index(string uri, bool related)
        {
            var init = await loadKit(AppInit.conf.Eporner);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            string memKey = $"eporner:view:{uri}";
            if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
                if (rch.IsNotSupport("web", out string rch_error))
                    return OnError(rch_error);

                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                stream_links = await EpornerTo.StreamLinks($"{host}/epr/vidosik", init.corsHost(), uri, 
                               htmlurl => rch.enable ? rch.Get(init.cors(htmlurl), httpHeaders(init)) : HttpClient.Get(init.cors(htmlurl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)), 
                               jsonurl => rch.enable ? rch.Get(init.cors(jsonurl), httpHeaders(init)) : HttpClient.Get(init.cors(jsonurl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)));

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
                return OnResult(stream_links?.recomends, null, plugin: init.plugin, total_pages: 1);

            return OnResult(stream_links, init, proxy);
        }
    }
}

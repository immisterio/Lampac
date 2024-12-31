using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
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
        async public Task<ActionResult> Index(string uri, bool related)
        {
            var init = AppInit.conf.Xhamster.Clone();

            if (!init.enable)
                return OnError("disable");

            if (NoAccessGroup(init, out string error_msg))
                return OnError(error_msg, false);

            var proxyManager = new ProxyManager("xmr", init);
            var proxy = proxyManager.Get();

            string memKey = $"xhamster:view:{uri}";
            if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
                if (rch.IsNotSupport("web", out string rch_error))
                    return OnError(rch_error, false);

                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                stream_links = await XhamsterTo.StreamLinks($"{host}/xmr/vidosik", init.corsHost(), uri, url =>
                    rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                );

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                {
                    if (IsRhubFallback(init))
                        goto reset;

                    return OnError("stream_links", proxyManager, !rch.enable);
                }

                if (!rch.enable)
                    proxyManager.Success();

                hybridCache.Set(memKey, stream_links, cacheTime(20));
            }

            if (related)
                return OnResult(stream_links?.recomends, null, plugin: "xmr", total_pages: 1);

            return OnResult(stream_links, init, proxy, plugin: "xmr");
        }
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;
using Shared.Model.Online;

namespace Lampac.Controllers.Ebalovo
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("elo/vidosik")]
        async public Task<ActionResult> Index(string uri, bool related)
        {
            var init = await loadKit(AppInit.conf.Ebalovo);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return OnError(rch_error);

            string memKey = rch.ipkey($"ebalovo:view:{uri}", proxyManager);
            if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await EbalovoTo.StreamLinks($"{host}/elo/vidosik", init.corsHost(), uri,
                    url => 
                    {
                        return rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : HttpClient.Get(init.cors(url), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                    },
                    async location => 
                    {
                        if (rch.enable)
                        {
                            var res = await rch.Headers(init.cors(location), null, httpHeaders(init, HeadersModel.Init(
                                ("referer", $"{init.host}/")
                            )));

                            return res.currentUrl;
                        }

                        return await HttpClient.GetLocation(init.cors(location), timeoutSeconds: 8, proxy: proxy, referer: $"{init.host}/", headers: httpHeaders(init));
                    }
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
                return OnResult(stream_links?.recomends, null, plugin: "elo", total_pages: 1);

            return OnResult(stream_links, init, proxy);
        }
    }
}

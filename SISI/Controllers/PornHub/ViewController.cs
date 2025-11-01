using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.PornHub
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("phub/vidosik")]
        async public ValueTask<ActionResult> Index(string vkey, bool related)
        {
            var init = await loadKit(AppInit.conf.PornHub);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web", out string rch_error))
                return OnError(rch_error);

            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            string memKey = $"phub:vidosik:{vkey}";
            return await InvkSemaphore(memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
                {
                    reset:
                    stream_links = await PornHubTo.StreamLinks("phub/vidosik", "phub", init.corsHost(), vkey, url =>
                        rch.enable 
                            ? rch.Get(init.cors(url), httpHeaders(init)) 
                            : Http.Get(init.cors(url), httpversion: 2, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init))
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
                    return OnResult(stream_links?.recomends, null, plugin: init.plugin, total_pages: 1);

                return OnResult(stream_links, init, proxy);
            });
        }


        [HttpGet]
        [Route("phubprem/vidosik")]
        async public ValueTask<ActionResult> Prem(string vkey, bool related)
        {
            var init = await loadKit(AppInit.conf.PornHubPremium);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            string memKey = $"phubprem:vidosik:{vkey}";
            if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await PornHubTo.StreamLinks("phubprem/vidosik", "phubprem", init.corsHost(), vkey, url => Http.Get(init.cors(url), httpversion: 2, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init, HeadersModel.Init("cookie", init.cookie))));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, stream_links, cacheTime(20, init: init));
            }

            if (related)
                return OnResult(stream_links?.recomends, null, plugin: init.plugin, total_pages: 1);

            return OnResult(stream_links, init, proxy);
        }
    }
}

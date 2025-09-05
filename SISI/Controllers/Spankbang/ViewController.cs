using Microsoft.AspNetCore.Mvc;
using Shared.PlaywrightCore;

namespace SISI.Controllers.Spankbang
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("sbg/vidosik")]
        async public ValueTask<ActionResult> Index(string uri, bool related)
        {
            var init = await loadKit(AppInit.conf.Spankbang);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            string memKey = $"spankbang:view:{uri}";

            return await InvkSemaphore(memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
                {
                    reset:
                    stream_links = await SpankbangTo.StreamLinks("sbg/vidosik", init.corsHost(), uri, url =>
                    {
                        if (rch.enable)
                            return rch.Get(init.cors(url), httpHeaders(init));

                        if (init.priorityBrowser == "http")
                            return Http.Get(url, httpversion: 2, timeoutSeconds: 8, headers: httpHeaders(init), proxy: proxy.proxy);

                        return PlaywrightBrowser.Get(init, url, httpHeaders(init), proxy.data);
                    });

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

                return OnResult(stream_links, init, proxy.proxy);
            });
        }
    }
}

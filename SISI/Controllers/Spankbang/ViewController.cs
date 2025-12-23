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

            return await SemaphoreResult($"spankbang:view:{uri}", async e =>
            {
                reset:
                if (rch.enable == false)
                    await e.semaphore.WaitAsync();

                if (!hybridCache.TryGetValue(e.key, out StreamItem stream_links))
                {
                    stream_links = await SpankbangTo.StreamLinks("sbg/vidosik", init.corsHost(), uri, url =>
                    {
                        if (rch.enable)
                            return rch.Get(init.cors(url), httpHeaders(init));

                        if (init.priorityBrowser == "http")
                            return Http.Get(init.cors(url), httpversion: 2, timeoutSeconds: 8, headers: httpHeaders(init), proxy: proxy);

                        return PlaywrightBrowser.Get(init, init.cors(url), httpHeaders(init), proxy_data);
                    });

                    if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    {
                        if (IsRhubFallback(init))
                            goto reset;

                        return OnError("stream_links", proxyManager);
                    }

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(e.key, stream_links, cacheTime(20, init: init));
                }

                if (related)
                    return OnResult(stream_links?.recomends, null, plugin: init.plugin, total_pages: 1);

                return OnResult(stream_links, init, proxy);
            });
        }
    }
}

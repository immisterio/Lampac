using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Xvideos
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("xds/vidosik")]
        async public ValueTask<ActionResult> Index(string uri, bool related)
        {
            var init = await loadKit(AppInit.conf.Xvideos);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            return await SemaphoreResult($"xvideos:view:{uri}", async e =>
            {
                reset:
                if (rch.enable == false)
                    await e.semaphore.WaitAsync();

                if (!hybridCache.TryGetValue(e.key, out StreamItem stream_links))
                {
                    stream_links = await XvideosTo.StreamLinks("xds/vidosik", $"{host}/xds/stars", init.corsHost(), uri, url =>
                        rch.enable
                            ? rch.Get(init.cors(url), httpHeaders(init))
                            : Http.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                    );

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

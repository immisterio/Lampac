using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.XvideosRED
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("xdsred/vidosik")]
        async public ValueTask<ActionResult> Index(string uri, bool related)
        {
            var init = await loadKit(AppInit.conf.XvideosRED);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            string memKey = $"xdsred:view:{uri}";

            return await InvkSemaphore(memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
                {
                    stream_links = await XvideosTo.StreamLinks("xdsred/vidosik", "xdsred/stars", init.corsHost(), uri,
                        url => Http.Get(url, cookie: init.cookie, timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                    );

                    if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                        return OnError("stream_links", proxyManager);

                    proxyManager.Success();
                    hybridCache.Set(memKey, stream_links, cacheTime(20, init: init));
                }

                if (related)
                    return OnResult(stream_links?.recomends, null, plugin: init.plugin, total_pages: 1);

                return OnResult(stream_links, init, proxy);
            });
        }
    }
}

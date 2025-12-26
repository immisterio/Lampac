using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.XvideosRED
{
    public class ViewController : BaseSisiController
    {
        public ViewController() : base(AppInit.conf.XvideosRED) { }

        [HttpGet]
        [Route("xdsred/vidosik")]
        async public ValueTask<ActionResult> Index(string uri, bool related)
        {
            if (await IsRequestBlocked(rch: false))
                return badInitMsg;

            return await InvkSemaphore($"xdsred:view:{uri}", async key =>
            {
                if (!hybridCache.TryGetValue(key, out StreamItem stream_links))
                {
                    stream_links = await XvideosTo.StreamLinks("xdsred/vidosik", "xdsred/stars", init.corsHost(), uri,
                        url => Http.Get(url, cookie: init.cookie, timeoutSeconds: init.httptimeout, proxy: proxy, headers: httpHeaders(init))
                    );

                    if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                        return OnError("stream_links", proxyManager);

                    proxyManager.Success();
                    hybridCache.Set(key, stream_links, cacheTime(20));
                }

                if (related)
                    return OnResult(stream_links?.recomends, null, total_pages: 1);

                return OnResult(stream_links);
            });
        }
    }
}

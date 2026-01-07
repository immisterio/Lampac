using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.XvideosRED
{
    public class ViewController : BaseSisiController
    {
        public ViewController() : base(AppInit.conf.XvideosRED) { }

        [HttpGet]
        [Route("xdsred/vidosik")]
        async public Task<ActionResult> Index(string uri, bool related)
        {
            if (await IsRequestBlocked(rch: false))
                return badInitMsg;

            return await InvkSemaphore($"xdsred:view:{uri}", async key =>
            {
                if (!hybridCache.TryGetValue(key, out StreamItem stream_links))
                {
                    string url = XvideosTo.StreamLinksUri("xdsred/stars", init.corsHost(), uri);
                    if (url == null)
                        return OnError("stream_links");

                    string html = await Http.Get(url, cookie: init.cookie, timeoutSeconds: init.httptimeout, proxy: proxy, headers: httpHeaders(init));
                    if (html == null)
                        return OnError("stream_links");

                    stream_links = XvideosTo.StreamLinks(html, "xdsred/vidosik", "xdsred/stars");

                    if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                        return OnError("stream_links", refresh_proxy: true);

                    proxyManager?.Success();
                    hybridCache.Set(key, stream_links, cacheTime(20));
                }

                if (related)
                    return await PlaylistResult(stream_links?.recomends, null, total_pages: 1);

                return OnResult(stream_links);
            });
        }
    }
}

using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Xhamster
{
    public class ViewController : BaseSisiController
    {
        public ViewController() : base(AppInit.conf.Xhamster) { }

        [HttpGet]
        [Route("xmr/vidosik")]
        async public Task<ActionResult> Index(string uri, bool related)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<StreamItem>($"xhamster:view:{uri}", 20, async e =>
            {
                string targetHost = init.corsHost();
                string url = XhamsterTo.StreamLinksUri(targetHost, uri);

                if (url == null)
                    return e.Fail("uri");

                ReadOnlySpan<char> html = await httpHydra.Get(url);

                var stream_links = XhamsterTo.StreamLinks(targetHost, "xmr/vidosik", html);

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return e.Fail("stream_links", refresh_proxy: true);

                return e.Success(stream_links);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            if (related)
                return await PlaylistResult(cache.Value?.recomends, null, total_pages: 1);

            return OnResult(cache);
        }
    }
}

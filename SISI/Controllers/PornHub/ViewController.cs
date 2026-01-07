using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.PornHub
{
    public class ViewController : BaseSisiController
    {
        public ViewController() : base(AppInit.conf.PornHub) { }

        [HttpGet]
        [Route("phub/vidosik")]
        async public Task<ActionResult> Index(string vkey, bool related)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<StreamItem>($"phub:vidosik:{vkey}", 20, async e =>
            {
                string url = PornHubTo.StreamLinksUri(init.corsHost(), vkey);
                if (url == null)
                    return e.Fail("vkey");

                ReadOnlySpan<char> html = await httpHydra.Get(url);

                var stream_links = PornHubTo.StreamLinks(html, "phub/vidosik", "phub");

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

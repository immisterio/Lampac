using Microsoft.AspNetCore.Mvc;
using Shared.PlaywrightCore;

namespace SISI.Controllers.Spankbang
{
    public class ViewController : BaseSisiController
    {
        public ViewController() : base(AppInit.conf.Spankbang) { }

        [HttpGet]
        [Route("sbg/vidosik")]
        async public Task<ActionResult> Index(string uri, bool related)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<StreamItem>($"spankbang:view:{uri}", 20, async e =>
            {
                string url = SpankbangTo.StreamLinksUri(init.corsHost(), uri);
                if (url == null)
                    return e.Fail("uri");

                ReadOnlySpan<char> html;

                if (rch?.enable == true || init.priorityBrowser == "http")
                    html = await httpHydra.Get(url);
                else
                    html = await PlaywrightBrowser.Get(init, url, httpHeaders(init), proxy_data);

                var stream_links = SpankbangTo.StreamLinks("sbg/vidosik", html);

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

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
                var stream_links = await SpankbangTo.StreamLinks("sbg/vidosik", init.corsHost(), uri, url =>
                {
                    if (rch?.enable == true || init.priorityBrowser == "http")
                        return httpHydra.Get(url);

                    return PlaywrightBrowser.Get(init, init.cors(url), httpHeaders(init), proxy_data);
                });

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

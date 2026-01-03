using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Xvideos
{
    public class ViewController : BaseSisiController
    {
        public ViewController() : base(AppInit.conf.Xvideos) { }

        [HttpGet]
        [Route("xds/vidosik")]
        async public Task<ActionResult> Index(string uri, bool related)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<StreamItem>($"xvideos:view:{uri}", 20, async e =>
            {
                var stream_links = await XvideosTo.StreamLinks("xds/vidosik", $"{host}/xds/stars", init.corsHost(), uri, 
                    url => httpHydra.Get(url)
                );

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

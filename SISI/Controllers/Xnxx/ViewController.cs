using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Xnxx
{
    public class ViewController : BaseSisiController
    {
        public ViewController() : base(AppInit.conf.Xnxx) { }

        [HttpGet]
        [Route("xnx/vidosik")]
        async public ValueTask<ActionResult> Index(string uri, bool related)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<StreamItem>($"xnxx:view:{uri}", 20, async e =>
            {
                var stream_links = await XnxxTo.StreamLinks("xnx/vidosik", init.corsHost(), uri, 
                    url => httpHydra.Get(url)
                );

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return e.Fail("stream_links", refresh_proxy: true);

                return e.Success(stream_links);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            if (related)
                return OnResult(cache.Value?.recomends, null, total_pages: 1);

            return OnResult(cache);
        }
    }
}

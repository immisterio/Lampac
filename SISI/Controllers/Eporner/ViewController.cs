using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Eporner
{
    public class ViewController : BaseSisiController
    {
        public ViewController() : base(AppInit.conf.Eporner) { }

        [HttpGet]
        [Route("epr/vidosik")]
        async public Task<ActionResult> Index(string uri, bool related)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<StreamItem>(ipkey($"eporner:view:{uri}"), 20, async e =>
            {
                var stream_links = await EpornerTo.StreamLinks("epr/vidosik", init.corsHost(), uri,
                    htmlurl => httpHydra.Get(htmlurl),
                    jsonurl => httpHydra.Get(jsonurl)
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

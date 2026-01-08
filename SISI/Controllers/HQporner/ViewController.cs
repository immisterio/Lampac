using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.HQporner
{
    public class ViewController : BaseSisiController
    {
        public ViewController() : base(AppInit.conf.HQporner) { }

        [HttpGet]
        [Route("hqr/vidosik")]
        async public ValueTask<ActionResult> Index(string uri)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<Dictionary<string, string>>(ipkey($"HQporner:view:{uri}"), 20, async e =>
            {
                var stream_links = await HQpornerTo.StreamLinks(httpHydra, init.corsHost(), uri);

                if (stream_links == null || stream_links.Count == 0)
                    return e.Fail("stream_links", refresh_proxy: true);

                return e.Success(stream_links);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return OnResult(cache);
        }
    }
}

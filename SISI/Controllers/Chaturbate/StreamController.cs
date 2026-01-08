using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Chaturbate
{
    public class StreamController : BaseSisiController
    {
        public StreamController() : base(AppInit.conf.Chaturbate) { }

        [HttpGet]
        [Route("chu/potok")]
        async public ValueTask<ActionResult> Index(string baba)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<Dictionary<string, string>>($"chaturbate:stream:{baba}", 10, async e =>
            {
                string url = ChaturbateTo.StreamLinksUri(init.corsHost(), baba);
                if (url == null)
                    return e.Fail("baba");

                Dictionary<string, string> stream_links = null;

                await httpHydra.GetSpan(url, span => 
                {
                    stream_links = ChaturbateTo.StreamLinks(span);
                });

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

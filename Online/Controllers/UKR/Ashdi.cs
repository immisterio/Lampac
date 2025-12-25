using Microsoft.AspNetCore.Mvc;

namespace Online.Controllers
{
    public class Ashdi : BaseOnlineController
    {
        public Ashdi() : base(AppInit.conf.Ashdi) { }

        [HttpGet]
        [Route("lite/ashdi")]
        async public ValueTask<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t = -1, int s = -1, bool rjson = false)
        {
            if (kinopoisk_id == 0)
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            var oninvk = new AshdiInvoke
            (
               host,
               init.corsHost(),
               ongettourl => rch.enable
                    ? rch.Get(init.cors(ongettourl), httpHeaders(init)) 
                    : Http.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init), statusCodeOK: false),
               streamfile => HostStreamProxy(streamfile),
               requesterror: () => proxyManager.Refresh(rch)
            );

            reset:
            var cache = await InvokeCacheResult($"ashdi:view:{kinopoisk_id}", 40, 
                () => oninvk.Embed(kinopoisk_id)
            );

            if (IsRhubFallback(cache))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, kinopoisk_id, title, original_title, t, s, vast: init.vast, rjson: rjson));
        }
    }
}

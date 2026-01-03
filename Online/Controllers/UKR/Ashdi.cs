using Microsoft.AspNetCore.Mvc;

namespace Online.Controllers
{
    public class Ashdi : BaseOnlineController
    {
        public Ashdi() : base(AppInit.conf.Ashdi) { }

        [HttpGet]
        [Route("lite/ashdi")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t = -1, int s = -1, bool rjson = false)
        {
            if (kinopoisk_id == 0)
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            var oninvk = new AshdiInvoke
            (
               host,
               init.corsHost(),
               httpHydra,
               streamfile => HostStreamProxy(streamfile),
               requesterror: () => proxyManager?.Refresh()
            );

            rhubFallback:
            var cache = await InvokeCacheResult($"ashdi:view:{kinopoisk_id}", 40, 
                () => oninvk.Embed(kinopoisk_id)
            );

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return await ContentTpl(cache, () => oninvk.Tpl(cache.Value, kinopoisk_id, title, original_title, t, s, vast: init.vast, rjson: rjson));
        }
    }
}

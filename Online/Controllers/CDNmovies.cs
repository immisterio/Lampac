using Microsoft.AspNetCore.Mvc;

namespace Online.Controllers
{
    public class CDNmovies : BaseOnlineController
    {
        public CDNmovies() : base(AppInit.conf.CDNmovies) { }

        [HttpGet]
        [Route("lite/cdnmovies")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t, int s = -1, int sid = -1, bool rjson = false)
        {
            if (kinopoisk_id == 0)
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            var oninvk = new CDNmoviesInvoke
            (
               host,
               init.corsHost(),
               httpHydra,
               onstreamtofile => HostStreamProxy(onstreamtofile),
               requesterror: () => proxyManager?.Refresh()
            );

            rhubFallback:
            var cache = await InvokeCacheResult($"cdnmovies:view:{kinopoisk_id}", 20, 
                () => oninvk.Embed(kinopoisk_id)
            );

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return await ContentTpl(cache, () => oninvk.Tpl(cache.Value, kinopoisk_id, title, original_title, t, s, sid, vast: init.vast, rjson: rjson));
        }
    }
}

using Microsoft.AspNetCore.Mvc;

namespace Online.Controllers
{
    public class CDNmovies : BaseOnlineController
    {
        public CDNmovies() : base(AppInit.conf.CDNmovies) { }

        [HttpGet]
        [Route("lite/cdnmovies")]
        async public ValueTask<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t, int s = -1, int sid = -1, bool rjson = false)
        {
            if (kinopoisk_id == 0)
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            var oninvk = new CDNmoviesInvoke
            (
               host,
               init.corsHost(),
               ongettourl => rch.enable 
                    ? rch.Get(init.cors(ongettourl), httpHeaders(init)) 
                    : Http.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               onstreamtofile => HostStreamProxy(onstreamtofile),
               requesterror: () => proxyManager.Refresh(rch)
            );

            reset:
            var cache = await InvokeCacheResult($"cdnmovies:view:{kinopoisk_id}", 20, 
                () => oninvk.Embed(kinopoisk_id)
            );

            if (IsRhubFallback(cache))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, kinopoisk_id, title, original_title, t, s, sid, vast: init.vast, rjson: rjson));
        }
    }
}

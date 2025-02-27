using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using System.Collections.Generic;
using Lampac.Models.LITE.CDNmovies;

namespace Lampac.Controllers.LITE
{
    public class CDNmovies : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/cdnmovies")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t, int s = -1, int sid = -1, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.CDNmovies);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (kinopoisk_id == 0)
                return OnError();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var oninvk = new CDNmoviesInvoke
            (
               host,
               init.corsHost(),
               ongettourl => rch.enable ? rch.Get(init.cors(ongettourl), httpHeaders(init)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               onstreamtofile => HostStreamProxy(init, onstreamtofile, proxy: proxy),
               requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
            );

            var cache = await InvokeCache<List<Voice>>($"cdnmovies:view:{kinopoisk_id}", cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(kinopoisk_id);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, kinopoisk_id, title, original_title, t, s, sid, vast: init.vast, rjson: rjson), origsource: origsource, gbcache: !rch.enable);
        }
    }
}

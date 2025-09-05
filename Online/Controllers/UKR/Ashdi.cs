using Microsoft.AspNetCore.Mvc;
using Shared.Models.Online.Ashdi;

namespace Online.Controllers
{
    public class Ashdi : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/ashdi")]
        async public ValueTask<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t = -1, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Ashdi);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (kinopoisk_id == 0)
                return OnError();

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var oninvk = new AshdiInvoke
            (
               host,
               init.corsHost(),
               ongettourl => rch.enable ? rch.Get(init.cors(ongettourl), httpHeaders(init)) : Http.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init), statusCodeOK: false),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy),
               requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
            );

            reset:
            var cache = await InvokeCache<EmbedModel>($"ashdi:view:{kinopoisk_id}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(kinopoisk_id);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, kinopoisk_id, title, original_title, t, s, vast: init.vast, rjson: rjson), origsource: origsource, gbcache: !rch.enable);
        }
    }
}

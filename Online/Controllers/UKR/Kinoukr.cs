using Microsoft.AspNetCore.Mvc;
using Shared.Models.Online.Eneyida;

namespace Online.Controllers
{
    public class Kinoukr : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinoukr")]
        async public ValueTask<ActionResult> Index(string title, string original_title, int clarification, int year, string t, int s = -1, string href = null, bool origsource = false, bool rjson = false, string source = null, string id = null)
        {
            var init = await loadKit(AppInit.conf.Kinoukr);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web", out string rch_error))
                return ShowError(rch_error);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var oninvk = new KinoukrInvoke
            (
               host,
               init.corsHost(),
               ongettourl => rch.enable ? rch.Get(init.cors(ongettourl), httpHeaders(init)) : Http.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               (url, data) => rch.enable ? rch.Post(init.cors(url), data, httpHeaders(init)) : Http.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               onstreamtofile => HostStreamProxy(init, onstreamtofile, proxy: proxy),
               requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
               //onlog: (l) => { Console.WriteLine(l); return string.Empty; }
            );

            if (string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.ToLower() == "kinoukr")
                    href = await InvokeCache($"kinoukr:source:{id}", cacheTime(180, init: init), () => oninvk.getIframeSource($"{init.host}/{id}"));
            }

            reset:
            var cache = await InvokeCache<EmbedModel>($"kinoukr:view:{title}:{original_title}:{year}:{href}:{clarification}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.EmbedKurwa(clarification, title, original_title, year, href);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, clarification, title, original_title, year, t, s, href, vast: init.vast, rjson: rjson), origsource: origsource, gbcache: !rch.enable);
        }
    }
}

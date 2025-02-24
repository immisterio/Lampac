using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Online.Eneyida;

namespace Lampac.Controllers.LITE
{
    public class Kinoukr : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinoukr")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, int t = -1, int s = -1, string href = null, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Kinoukr);
            if (IsBadInitialization(init, out ActionResult action, rch: true))
                return action;

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(original_title) || year == 0))
                return OnError();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            if (rch.IsNotSupport("web", out string rch_error))
                return ShowError(rch_error);

            var oninvk = new KinoukrInvoke
            (
               host,
               init.corsHost(),
               ongettourl => rch.enable ? rch.Get(init.cors(ongettourl), httpHeaders(init)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               (url, data) => rch.enable ? rch.Post(init.cors(url), data, httpHeaders(init)) : HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               onstreamtofile => HostStreamProxy(init, onstreamtofile, proxy: proxy),
               requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
               //onlog: (l) => { Console.WriteLine(l); return string.Empty; }
            );

            var cache = await InvokeCache<EmbedModel>($"kinoukr:view:{title}:{year}:{href}:{clarification}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(clarification == 1 ? title : original_title, year, href);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, clarification, title, original_title, year, t, s, href, vast: init.vast, rjson: rjson), origsource: origsource, gbcache: !rch.enable);
        }
    }
}

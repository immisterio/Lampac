using Microsoft.AspNetCore.Mvc;
using Shared.Models.Online.Redheadsound;

namespace Online.Controllers
{
    public class Redheadsound : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/redheadsound")]
        async public ValueTask<ActionResult> Index(string title, string original_title, int year, int clarification, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Redheadsound);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrWhiteSpace(title) || year == 0)
                return OnError();

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var oninvk = new RedheadsoundInvoke
            (
               host,
               init.corsHost(),
               ongettourl => rch.enable ? rch.Get(init.cors(ongettourl), httpHeaders(init)) : Http.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               (url, data) => rch.enable ? rch.Post(init.cors(url), data, httpHeaders(init)) : Http.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy),
               requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
            );

            reset:
            var cache = await InvokeCache<EmbedModel>($"redheadsound:view:{title}:{year}", cacheTime(30, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(title, year);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, title, vast: init.vast, rjson: rjson), origsource: origsource, gbcache: !rch.enable);
        }
    }
}

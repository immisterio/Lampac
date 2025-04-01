using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Online.Collaps;
using Shared.Model.Online;

namespace Lampac.Controllers.LITE
{
    public class Collaps : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/collaps")]
        [Route("lite/collaps-dash")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Collaps, (j, i, c) =>
            {
                if (j.ContainsKey("two"))
                    i.two = c.two;
                if (j.ContainsKey("dash"))
                    i.dash = c.dash;
                return i;
            });

            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return OnError();

            string module = HttpContext.Request.Path.Value.StartsWith("/lite/collaps-dash") ? "dash" : "hls";
            if (module == "dash")
                init.dash = true;
            else if (init.two)
                init.dash = false;

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var oninvk = new CollapsInvoke
            (
               host,
               init.corsHost(),
               init.dash,
               ongettourl => rch.enable ? rch.Get(init.cors(ongettourl), httpHeaders(init)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               onstreamtofile => rch.enable ? onstreamtofile : HostStreamProxy(init, onstreamtofile, proxy: proxy),
               requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
            );

            var cache = await InvokeCache<EmbedModel>($"collaps:view:{imdb_id}:{kinopoisk_id}", cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(imdb_id, kinopoisk_id);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => 
            {
                string html = oninvk.Html(cache.Value, imdb_id, kinopoisk_id, title, original_title, s, vast: init.vast, rjson: rjson, headers: httpHeaders(init.host, HeadersModel.Init(init.headers_stream)));
                if (module == "dash")
                    html = html.Replace("lite/collaps", "lite/collaps-dash");

                return html;

            }, origsource: origsource, gbcache: !rch.enable);
        }
    }
}

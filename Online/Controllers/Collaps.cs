using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Collaps;

namespace Online.Controllers
{
    public class Collaps : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/collaps")]
        [Route("lite/collaps-dash")]
        async public ValueTask<ActionResult> Index(long orid, string imdb_id, long kinopoisk_id, string title, string original_title, int s = -1, bool origsource = false, bool rjson = false, bool similar = false)
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

            if (similar || (orid == 0 && kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id)))
                return await Search(title, origsource, rjson);

            string module = HttpContext.Request.Path.Value.StartsWith("/lite/collaps-dash") ? "dash" : "hls";
            if (module == "dash")
                init.dash = true;
            else if (init.two)
                init.dash = false;

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var oninvk = new CollapsInvoke
            (
               host,
               init.corsHost(),
               init.dash,
               ongettourl => rch.enable ? rch.Get(init.cors(ongettourl), httpHeaders(init)) : Http.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               onstreamtofile => rch.enable ? onstreamtofile : HostStreamProxy(init, onstreamtofile, proxy: proxy),
               requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
            );

            reset:
            var cache = await InvokeCache<EmbedModel>($"collaps:view:{imdb_id}:{kinopoisk_id}:{orid}", cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(imdb_id, kinopoisk_id, orid);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => 
            {
                string html = oninvk.Html(cache.Value, imdb_id, kinopoisk_id, orid, title, original_title, s, vast: init.vast, rjson: rjson, headers: httpHeaders(init.host, init.headers_stream));
                if (module == "dash")
                    html = html.Replace("lite/collaps", "lite/collaps-dash");

                return html;

            }, origsource: origsource, gbcache: !rch.enable);
        }


        [HttpGet]
        [Route("lite/collaps-search")]
        async public ValueTask<ActionResult> Search(string title, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Collaps);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var cache = await InvokeCache<ResultSearch[]>($"collaps:search:{title}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string uri = $"{init.apihost}/list?token={init.token}&name={HttpUtility.UrlEncode(title)}";
                var root = rch.enable ? await rch.Get<JObject>(uri) : await Http.Get<JObject>(uri, timeoutSeconds: 8, proxy: proxy);
                if (root == null || !root.ContainsKey("results"))
                    return res.Fail("results");

                return root["results"].ToObject<ResultSearch[]>();
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () =>
            {
                var stpl = new SimilarTpl(cache.Value.Length);

                foreach (var j in cache.Value)
                {
                    string uri = $"{host}/lite/collaps?orid={j.id}";
                    stpl.Append(j.name ?? j.origin_name, j.year.ToString(), string.Empty, uri, PosterApi.Size(j.poster));
                }

                return rjson ? stpl.ToJson() : stpl.ToHtml();

            }, origsource: origsource, gbcache: !rch.enable);
        }
    }
}

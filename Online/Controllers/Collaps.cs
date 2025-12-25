using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Collaps;
using Shared.Models.Online.Settings;

namespace Online.Controllers
{
    public class Collaps : BaseOnlineController<CollapsSettings>
    {
        CollapsInvoke oninvk;

        public Collaps() : base(AppInit.conf.Collaps) 
        {
            loadKitInitialization = (j, i, c) =>
            {
                if (j.ContainsKey("two"))
                    i.two = c.two;
                if (j.ContainsKey("dash"))
                    i.dash = c.dash;

                return i;
            };

            requestInitialization = () => 
            {
                oninvk = new CollapsInvoke
                (
                   host,
                   init.corsHost(),
                   init.dash,
                   ongettourl => rch.enable 
                        ? rch.Get(init.cors(ongettourl), httpHeaders(init)) 
                        : Http.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
                   onstreamtofile => rch.enable ? onstreamtofile : HostStreamProxy(onstreamtofile),
                   requesterror: () => proxyManager.Refresh(rch)
                );
            };
        }

        [HttpGet]
        [Route("lite/collaps")]
        [Route("lite/collaps-dash")]
        async public ValueTask<ActionResult> Index(long orid, string imdb_id, long kinopoisk_id, string title, string original_title, int s = -1, bool rjson = false, bool similar = false)
        {
            if (await IsRequestBlocked( rch: true))
                return badInitMsg;

            if (similar || (orid == 0 && kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id)))
                return await Search(title, rjson);

            string module = HttpContext.Request.Path.Value.StartsWith("/lite/collaps-dash") ? "dash" : "hls";
            if (module == "dash")
                init.dash = true;
            else if (init.two)
                init.dash = false;

            reset:
            var cache = await InvokeCacheResult($"collaps:view:{imdb_id}:{kinopoisk_id}:{orid}", 20, 
                () => oninvk.Embed(imdb_id, kinopoisk_id, orid)
            );

            if (IsRhubFallback(cache))
                goto reset;

            return OnResult(cache, () => 
            {
                string html = oninvk.Html(cache.Value, imdb_id, kinopoisk_id, orid, title, original_title, s, vast: init.vast, rjson: rjson, headers: httpHeaders(init.host, init.headers_stream));
                if (module == "dash")
                    html = html.Replace("lite/collaps", "lite/collaps-dash");

                return html;
            });
        }


        [HttpGet]
        [Route("lite/collaps-search")]
        async public ValueTask<ActionResult> Search(string title, bool rjson = false)
        {
            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            reset:
            var cache = await InvokeCacheResult<ResultSearch[]>($"collaps:search:{title}", 40, async e =>
            {
                string uri = $"{init.apihost}/list?token={init.token}&name={HttpUtility.UrlEncode(title)}";

                var root = rch.enable 
                    ? await rch.Get<JObject>(uri) 
                    : await Http.Get<JObject>(uri, timeoutSeconds: 8, proxy: proxy);

                if (root == null || !root.ContainsKey("results"))
                    return e.Fail("results", refresh_proxy: true);

                return e.Success(root["results"].ToObject<ResultSearch[]>());
            });

            if (IsRhubFallback(cache))
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

            });
        }
    }
}

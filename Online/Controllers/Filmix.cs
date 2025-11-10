using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Filmix;

namespace Online.Controllers
{
    public class Filmix : BaseOnlineController
    {
        #region filmixpro
        [HttpGet]
        [AllowAnonymous]
        [Route("lite/filmixpro")]
        async public Task<ActionResult> Pro()
        {
            var token_request = await Http.Get<JObject>($"{AppInit.conf.Filmix.corsHost()}/api/v2/token_request?user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_vendor=Xiaomi&user_dev_token=", useDefaultHeaders: false);

            if (token_request == null)
                return Content($"нет доступа к {AppInit.conf.Filmix.corsHost()}", "text/html; charset=utf-8");

            string html = "1. Откройте <a href='https://filmix.my/consoles'>https://filmix.my/consoles</a> <br>";
            html += $"2. Введите код <b>{token_request.Value<string>("user_code")}</b><br>";
            html += $"<br><br><br>Добавьте в init.conf<br><br>";
            html += "\"Filmix\": {<br>&nbsp;&nbsp;\"token\": \"" + token_request.Value<string>("code") + "\",<br>&nbsp;&nbsp;\"pro\": true<br>}";

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("lite/filmix")]
        async public ValueTask<ActionResult> Index(string title, string original_title, int clarification, int year, int postid, int t, int? s = null, bool origsource = false, bool rjson = false, bool similar = false, string source = null, string id = null)
        {
            if (postid == 0 && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.ToLower() is "filmix" or "filmixapp")
                {
                    if (!int.TryParse(id, out postid))
                        int.TryParse(Regex.Match(id, "/([0-9]+)-").Groups[1].Value, out postid);
                }
            }

            var init = await loadKit(AppInit.conf.Filmix, (j, i, c) =>
            {
                if (j.ContainsKey("reserve"))
                    i.reserve = c.reserve;

                i.pro = c.pro;
                i.tokens = c.tokens;
                return i;
            });

            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("cors,web", out string rch_error))
                return ShowError(rch_error);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            string token = init.token;
            if (init.tokens != null && init.tokens.Length > 1)
                token = init.tokens[Random.Shared.Next(0, init.tokens.Length)];

            reset:

            var oninvk = new FilmixInvoke
            (
               init,
               host,
               token,
               ongettourl => rch.enable 
                    ? rch.Get(init.cors(ongettourl), httpHeaders(init), useDefaultHeaders: false) 
                    : Http.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init), useDefaultHeaders: false),
               (url, data, head) => rch.enable 
                    ? rch.Post(init.cors(url), data, (head != null ? head : httpHeaders(init)), useDefaultHeaders: false) 
                    : Http.Post(init.cors(url), data, timeoutSeconds: 8, headers: head != null ? head : httpHeaders(init), useDefaultHeaders: false),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy),
               requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } },
               rjson: rjson
            );

            if (postid == 0)
            {
                var search = await InvokeCache<SearchResult>($"filmix:search:{title}:{original_title}:{year}:{clarification}:{similar}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    return await oninvk.Search(title, original_title, clarification, year, similar);
                });

                if (!search.IsSuccess)
                    return OnError(search.ErrorMsg);

                if (search.Value.id == 0)
                    return ContentTo(rjson ? search.Value.similars.Value.ToJson() : search.Value.similars.Value.ToHtml());

                postid = search.Value.id;
            }

            var cache = await InvokeCache<RootObject>($"filmix:post:{postid}:{token}", cacheTime(20, init: init), rch.enable ? null : proxyManager, onget: async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Post(postid);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, init.pro, postid, title, original_title, t, s, vast: init.vast), origsource: origsource, gbcache: !rch.enable);
        }
    }
}

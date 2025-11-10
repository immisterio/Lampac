using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.KinoPub;
using System.Net;

namespace Online.Controllers
{
    public class KinoPub : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager(AppInit.conf.KinoPub);

        static CookieContainer cookies = new CookieContainer();

        #region kinopubpro
        [HttpGet]
        [AllowAnonymous]
        [Route("lite/kinopubpro")]
        async public Task<ActionResult> Pro(string code, string name)
        {
            var proxy = proxyManager.Get();
            var init = AppInit.conf.KinoPub;
            var headers = httpHeaders(init);

            if (string.IsNullOrWhiteSpace(code))
            {
                var token_request = await Http.Post<JObject>($"{init.corsHost()}/oauth2/device?grant_type=device_code&client_id=xbmc&client_secret=cgg3gtifu46urtfp2zp1nqtba0k2ezxh", "", proxy: proxy, headers: httpHeaders(init), httpversion: 2);

                if (token_request == null)
                    return Content($"нет доступа к {init.corsHost()}", "text/html; charset=utf-8");

                string html = "1. Откройте <a href='https://kino.pub/device'>https://kino.pub/device</a> <br>";
                html += $"2. Введите код активации <b>{token_request.Value<string>("user_code")}</b><br>";
                html += $"3. Когда на сайте kino.pub появится \"Ожидание устройства\", нажмите кнопку \"Проверить активацию\" которая ниже</b>";

                html += $"<br><br><a href='/lite/kinopubpro?code={token_request.Value<string>("code")}&name={name}'><button>Проверить активацию</button></a>";

                return Content(html, "text/html; charset=utf-8");
            }
            else
            {
                var device_token = await Http.Post<JObject>($"{init.corsHost()}/oauth2/device?grant_type=device_token&client_id=xbmc&client_secret=cgg3gtifu46urtfp2zp1nqtba0k2ezxh&code={code}", "", proxy: proxy, headers: httpHeaders(init), httpversion: 2);
                if (device_token == null || string.IsNullOrWhiteSpace(device_token.Value<string>("access_token")))
                    return LocalRedirect("/lite/kinopubpro");

                if (!string.IsNullOrEmpty(name))
                    await Http.Post($"{init.corsHost()}/v1/device/notify?access_token={device_token.Value<string>("access_token")}", $"&title={name}", proxy: proxy, headers: httpHeaders(init), httpversion: 2);

                return Content("Добавьте в init.conf<br><br>\"KinoPub\": {<br>&nbsp;&nbsp;\"enable\": true,<br>&nbsp;&nbsp;\"token\": \"" + device_token.Value<string>("access_token") + "\"<br>}", "text/html; charset=utf-8");
            }
        }
        #endregion

        [HttpGet]
        [Route("lite/kinopub")]
        async public ValueTask<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int year, int clarification, int postid, int s = -1, int t = -1, string codec = null, bool origsource = false, bool rjson = false, bool similar = false, string source = null, string id = null)
        {
            if (postid == 0 && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.ToLower() == "kinopub")
                    int.TryParse(id, out postid);
            }

            var init = await loadKit(AppInit.conf.KinoPub, (j, i, c) =>
            {
                if (j.ContainsKey("filetype"))
                    i.filetype = c.filetype;
                i.tokens = c.tokens;
                return i;
            });

            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            var proxy = proxyManager.Get();

            string token = init.token;
            if (init.tokens != null && init.tokens.Length > 1)
                token = init.tokens[Random.Shared.Next(0, init.tokens.Length)];

            var oninvk = new KinoPubInvoke
            (
               host,
               init.corsHost(),
               token,
               ongettourl => rch.enable ? rch.Get(init.cors(ongettourl), httpHeaders(init)) : 
                                          Http.Get(init.cors(ongettourl), httpversion: 2, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init), cookieContainer: cookies),
               (stream, filepath) => HostStreamProxy(init, stream, proxy: proxy),
               requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
            );

            if (postid == 0)
            {
                var search = await InvokeCache<SearchResult>($"kinopub:search:{title}:{year}:{clarification}:{imdb_id}:{kinopoisk_id}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    return await oninvk.Search(title, original_title, year, clarification, imdb_id, kinopoisk_id);
                });

                if (!search.IsSuccess)
                    return OnError(search.ErrorMsg);

                if (similar || search.Value.id == 0)
                {
                    if (search.Value.similars == null)
                        return OnError();

                    return ContentTo(rjson ? search.Value.similars.Value.ToJson() : search.Value.similars.Value.ToHtml());
                }

                postid = search.Value.id;
            }

            var cache = await InvokeCache<RootObject>($"kinopub:post:{postid}:{token}", cacheTime(10, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Post(postid);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, init.filetype, title, original_title, postid, s, t, codec, vast: init.vast, rjson: rjson), origsource: origsource, gbcache: !rch.enable);
        }


        [HttpGet]
        [Route("lite/kinopub/subtitles.json")]
        async public ValueTask<ActionResult> Subtitles(int mid)
        {
            var init = await loadKit(AppInit.conf.KinoPub, (j, i, c) =>
            {
                i.tokens = c.tokens;
                return i;
            });

            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var proxy = proxyManager.Get();

            string token = init.token;
            if (init.tokens != null && init.tokens.Length > 1)
                token = init.tokens[Random.Shared.Next(0, init.tokens.Length)];

            string uri = $"{init.corsHost()}/v1/items/media-links?mid={mid}&access_token={token}";

            var root = await InvokeCache($"kinopub:media-links:{mid}:{token}", cacheTime(20, init: init), 
                () => Http.Get<JObject>(uri, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init))
            );

            if (root == null || !root.ContainsKey("subtitles"))
            {
                proxyManager.Refresh();
                return ContentTo("[]");
            }

            var subs = root["subtitles"] as JArray;

            if (subs == null || subs.Count == 0)
                return ContentTo("[]");

            var tpl = new SubtitleTpl(subs.Count);

            foreach (var s in subs.OfType<JObject>())
            {
                try
                {
                    string lang = s.Value<string>("lang");
                    string url = s.Value<string>("url");

                    if (!string.IsNullOrEmpty(url))
                        tpl.Append(lang, HostStreamProxy(init, url, proxy: proxy));
                }
                catch { }
            }

            return ContentTo(tpl.ToJson());
        }
    }
}

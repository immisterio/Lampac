using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Settings;

namespace Online.Controllers
{
    public class KinoPub : BaseOnlineController<KinoPubSettings>
    {
        public KinoPub() : base(AppInit.conf.KinoPub) 
        {
            loadKitInitialization = (j, i, c) =>
            {
                if (j.ContainsKey("filetype"))
                    i.filetype = c.filetype;
                i.tokens = c.tokens;
                return i;
            };
        }

        #region kinopubpro
        [HttpGet]
        [AllowAnonymous]
        [Route("lite/kinopubpro")]
        async public Task<ActionResult> Pro(string code, string name)
        {
            var headers = httpHeaders(init);

            if (string.IsNullOrWhiteSpace(code))
            {
                var token_request = await httpHydra.Post<JObject>($"{init.corsHost()}/oauth2/device?grant_type=device_code&client_id=xbmc&client_secret=cgg3gtifu46urtfp2zp1nqtba0k2ezxh", "");

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
                var device_token = await httpHydra.Post<JObject>($"{init.corsHost()}/oauth2/device?grant_type=device_token&client_id=xbmc&client_secret=cgg3gtifu46urtfp2zp1nqtba0k2ezxh&code={code}", "");
                if (device_token == null || string.IsNullOrWhiteSpace(device_token.Value<string>("access_token")))
                    return LocalRedirect("/lite/kinopubpro");

                if (!string.IsNullOrEmpty(name))
                    await httpHydra.Post($"{init.corsHost()}/v1/device/notify?access_token={device_token.Value<string>("access_token")}", $"&title={name}");

                return Content("Добавьте в init.conf<br><br>\"KinoPub\": {<br>&nbsp;&nbsp;\"enable\": true,<br>&nbsp;&nbsp;\"token\": \"" + device_token.Value<string>("access_token") + "\"<br>}", "text/html; charset=utf-8");
            }
        }
        #endregion

        [HttpGet]
        [Route("lite/kinopub")]
        async public ValueTask<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int year, int clarification, int postid, int s = -1, int t = -1, string codec = null, bool rjson = false, bool similar = false, string source = null, string id = null)
        {
            if (postid == 0 && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.ToLower() == "kinopub")
                    int.TryParse(id, out postid);
            }

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            string token = init.token;
            if (init.tokens != null && init.tokens.Length > 1)
                token = init.tokens[Random.Shared.Next(0, init.tokens.Length)];

            var oninvk = new KinoPubInvoke
            (
               host,
               init.corsHost(),
               token,
               ongettourl => httpHydra.Get(ongettourl),
               (stream, filepath) => HostStreamProxy(stream),
               requesterror: () => proxyManager.Refresh(rch)
            );

            if (postid == 0)
            {
                var search = await InvokeCacheResult($"kinopub:search:{title}:{year}:{clarification}:{imdb_id}:{kinopoisk_id}", 40, 
                    () => oninvk.Search(title, original_title, year, clarification, imdb_id, kinopoisk_id)
                );

                if (!search.IsSuccess)
                    return OnError(search.ErrorMsg);

                if (similar || search.Value.id == 0)
                {
                    if (search.Value.similars == null)
                        return OnError();

                    return ContentTo(search.Value.similars.Value);
                }

                postid = search.Value.id;
            }

            var cache = await InvokeCacheResult($"kinopub:post:{postid}:{token}", 10, 
                () => oninvk.Post(postid)
            );

            return OnResult(cache, () => oninvk.Tpl(cache.Value, init.filetype, title, original_title, postid, s, t, codec, vast: init.vast, rjson: rjson));
        }


        [HttpGet]
        [Route("lite/kinopub/subtitles.json")]
        async public ValueTask<ActionResult> Subtitles(int mid)
        {
            if (await IsRequestBlocked(rch: true, rch_check: false))
                return badInitMsg;

            string token = init.token;
            if (init.tokens != null && init.tokens.Length > 1)
                token = init.tokens[Random.Shared.Next(0, init.tokens.Length)];

            string uri = $"{init.corsHost()}/v1/items/media-links?mid={mid}&access_token={token}";

            var root = await InvokeCache($"kinopub:media-links:{mid}:{token}", 20, 
                () => httpHydra.Get<JObject>(uri)
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
                    string url = s.Value<string>("url");

                    if (!string.IsNullOrEmpty(url))
                        tpl.Append(s.Value<string>("lang"), HostStreamProxy(url));
                }
                catch { }
            }

            return ContentTo(tpl.ToJson());
        }
    }
}

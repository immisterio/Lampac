using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Settings;

namespace Online.Controllers
{
    public class Filmix : BaseOnlineController<FilmixSettings>
    {
        public Filmix() : base(AppInit.conf.Filmix) 
        {
            loadKitInitialization = (j, i, c) =>
            {
                if (j.ContainsKey("reserve"))
                    i.reserve = c.reserve;

                i.pro = c.pro;
                i.tokens = c.tokens;
                return i;
            };
        }

        #region filmixpro
        [HttpGet]
        [AllowAnonymous]
        [Route("lite/filmixpro")]
        async public Task<ActionResult> Pro()
        {
            var token_request = await Http.Get<JObject>($"{init.corsHost()}/api/v2/token_request?user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_vendor=Xiaomi&user_dev_token=", proxy: proxy, useDefaultHeaders: false);

            if (token_request == null)
                return ContentTo($"нет доступа к {init.corsHost()}");

            string html = "1. Откройте <a href='https://filmix.my/consoles'>https://filmix.my/consoles</a> <br>";
            html += $"2. Введите код <b>{token_request.Value<string>("user_code")}</b><br>";
            html += $"<br><br><br>Добавьте в init.conf<br><br>";
            html += "\"Filmix\": {<br>&nbsp;&nbsp;\"token\": \"" + token_request.Value<string>("code") + "\",<br>&nbsp;&nbsp;\"pro\": true<br>}";

            return ContentTo(html);
        }
        #endregion

        [HttpGet]
        [Route("lite/filmix")]
        async public ValueTask<ActionResult> Index(string title, string original_title, int clarification, int year, int postid, int t, int? s = null, bool rjson = false, bool similar = false, string source = null, string id = null)
        {
            if (postid == 0 && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.ToLower() is "filmix" or "filmixapp")
                {
                    if (!int.TryParse(id, out postid))
                        int.TryParse(Regex.Match(id, "/([0-9]+)-").Groups[1].Value, out postid);
                }
            }

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            string token = init.token;
            if (init.tokens != null && init.tokens.Length > 1)
                token = init.tokens[Random.Shared.Next(0, init.tokens.Length)];

            var oninvk = new FilmixInvoke
            (
               init,
               host,
               token,
               ongettourl => httpHydra.Get(ongettourl, useDefaultHeaders: false),
               (url, data, head) => httpHydra.Post(url, data, addheaders: head, useDefaultHeaders: false),
               streamfile => HostStreamProxy(streamfile),
               requesterror: () => proxyManager.Refresh(rch),
               rjson: rjson
            );

            rhubFallback:

            if (postid == 0)
            {
                var search = await InvokeCacheResult($"filmix:search:{title}:{original_title}:{year}:{clarification}:{similar}", 40, 
                    () => oninvk.Search(title, original_title, clarification, year, similar)
                );

                if (!search.IsSuccess)
                    return OnError(search.ErrorMsg);

                if (search.Value.id == 0)
                    return ContentTo(search.Value.similars.Value);

                postid = search.Value.id;
            }

            var cache = await InvokeCacheResult($"filmix:post:{postid}:{token}", 20, 
                () => oninvk.Post(postid)
            );

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return OnResult(cache, () => oninvk.Tpl(cache.Value, init.pro, postid, title, original_title, t, s, vast: init.vast));
        }
    }
}

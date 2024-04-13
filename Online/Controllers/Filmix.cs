using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using Shared.Engine.Online;
using Online;
using Shared.Engine.CORE;
using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Controllers.LITE
{
    public class Filmix : BaseOnlineController
    {
        #region filmixpro
        [HttpGet]
        [Route("lite/filmixpro")]
        async public Task<ActionResult> Pro()
        {
            var token_request = await HttpClient.Get<JObject>($"{AppInit.conf.Filmix.corsHost()}/api/v2/token_request?user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_vendor=Xiaomi&user_dev_token=");

            if (token_request == null)
                return Content($"нет доступа к {AppInit.conf.Filmix.corsHost()}", "text/html; charset=utf-8");

            string html = "1. Откройте <a href='https://filmix.biz/consoles'>https://filmix.biz/consoles</a> <br>";
            html += $"2. Введите код <b>{token_request.Value<string>("user_code")}</b><br>";
            html += $"<br><br>В init.conf<br>";
            html += $"1. Укажите token <b>{token_request.Value<string>("code")}</b><br>";
            html += $"2. Измените \"pro\": false, на \"pro\": true, если у вас PRO аккаунт</b>";

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("lite/filmix")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, string original_language, int year, int postid, int t, int? s = null)
        {
            var init = AppInit.conf.Filmix;

            if (!init.enable)
                return OnError();

            if (original_language != "en")
                clarification = 1;

            var proxyManager = new ProxyManager("filmix", init);
            var proxy = proxyManager.Get();

            string token = init.token;
            if (init.tokens != null && init.tokens.Length > 1)
                token = init.tokens[Random.Shared.Next(0, init.tokens.Length)];

            string livehash = string.Empty;
            if (init.livehash && !string.IsNullOrEmpty(init.token))
                livehash = await getLiveHash();

            var oninvk = new FilmixInvoke
            (
               host,
               init.corsHost(),
               token,
               ongettourl => HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               (url, data, head) => HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, headers: httpHeaders(init, head)),
               streamfile => HostStreamProxy(init, replaceLink(livehash, streamfile), proxy: proxy)
            );

            if (postid == 0)
            {
                var res = await InvokeCache($"filmix:search:{title}:{original_title}:{clarification}", cacheTime(40), () => oninvk.Search(title, original_title, clarification, year));
                if (res == null || res.id == 0)
                    return Content(res?.similars ?? string.Empty);

                postid = res.id;
            }

            var player_links = await InvokeCache($"filmix:post:{postid}", cacheTime(20), () => oninvk.Post(postid), proxyManager, inmemory: true);
            if (player_links == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(player_links, init.pro, postid, title, original_title, t, s), "text/html; charset=utf-8");
        }


        async ValueTask<string> getLiveHash()
        {
            string memKey = $"filmix:ChangeLink:hashfimix";
            if (!memoryCache.TryGetValue(memKey, out string hash))
            {
                var init = AppInit.conf.Filmix;
                string json = await HttpClient.Get($"{init.corsHost()}/api/v2/post/170245?user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_token={init.token}&user_dev_vendor=Xiaomi");
                hash = Regex.Match(json?.Replace("\\", ""), "/s/([^/]+)/").Groups[1].Value;

                if (!string.IsNullOrWhiteSpace(hash))
                    memoryCache.Set(memKey, hash, DateTime.Now.AddHours(4));
            }

            return hash;
        }

        string replaceLink(string hash, string l)
        {
            if (string.IsNullOrEmpty(hash))
                return l;

            return Regex.Replace(l, "/s/[^/]+/", $"/s/{hash}/");
        }
    }
}

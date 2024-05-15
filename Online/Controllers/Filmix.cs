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
using Lampac.Models.LITE.Filmix;
using Shared.Model.Online.Filmix;
using Shared.Model.Online;
using System.Text;
using Lampac.Models.LITE;

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
            html += $"<br><br><br>Добавьте в init.conf<br><br>";
            html += "\"Filmix\": {<br>&nbsp;&nbsp;\"token\": \"" + token_request.Value<string>("code") + "\",<br>&nbsp;&nbsp;\"pro\": true<br>}";

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("lite/filmix")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, int postid, int t, int? s = null)
        {
            var init = AppInit.conf.Filmix.Clone();

            if (!init.enable)
                return OnError();

            var rch = new RchClient(HttpContext, host, init.rhub);
            var proxyManager = new ProxyManager("filmix", init);
            var proxy = proxyManager.Get();

            string token = init.token;
            if (init.tokens != null && init.tokens.Length > 1)
                token = init.tokens[Random.Shared.Next(0, init.tokens.Length)];

            #region filmix.tv
            if (!string.IsNullOrEmpty(init.user_apitv) && string.IsNullOrEmpty(init.token_apitv))
            {
                string accessToken = await InvokeCache("filmix:accessToken", TimeSpan.FromHours(8), async () => 
                {
                    var content = new System.Net.Http.StringContent($"{{\"user_name\":\"{init.user_apitv}\",\"user_passw\":\"{init.passwd_apitv}\"}}", Encoding.UTF8, "application/json"); ;
                    var jobject = await HttpClient.Post<JObject>("https://api.filmix.tv/api-fx/auth", content, timeoutSeconds: 8);
                    return jobject?.GetValue("accessToken")?.ToString();
                });

                if (!string.IsNullOrEmpty(accessToken))
                {
                    init.pro = true;
                    init.token_apitv = accessToken;
                }
            }

            string livehash = string.Empty;
            if (!init.rhub && (init.livehash || !string.IsNullOrEmpty(init.token_apitv)))
                livehash = await getLiveHash(init);
            #endregion

            var oninvk = new FilmixInvoke
            (
               host,
               init.corsHost(),
               token,
               ongettourl => init.rhub ? rch.Get(init.cors(ongettourl)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               (url, data, head) => init.rhub ? rch.Post(init.cors(url), data) : HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, headers: httpHeaders(init, head)),
               streamfile => HostStreamProxy(init, replaceLink(livehash, streamfile), proxy: proxy),
               requesterror: () => proxyManager.Refresh()
            );

            if (postid == 0)
            {
                var search = await InvokeCache<SearchResult>($"filmix:search:{title}:{original_title}:{clarification}", cacheTime(40, init: init), proxyManager, async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    return await oninvk.Search(title, original_title, clarification, year);
                });

                if (!search.IsSuccess)
                    return OnError(search.ErrorMsg);

                if (search.Value.id == 0)
                    return Content(search.Value.similars);

                postid = search.Value.id;
            }

            var cache = await InvokeCache<RootObject>($"filmix:post:{postid}", cacheTime(20, init: init), proxyManager, inmemory: true, onget: async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Post(postid);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, init.pro, postid, title, original_title, t, s));
        }


        async ValueTask<string> getLiveHash(FilmixSettings init)
        {
            string memKey = $"filmix:ChangeLink:hashfimix";
            if (!memoryCache.TryGetValue(memKey, out string hash))
            {
                if (!string.IsNullOrEmpty(init.token_apitv))
                {
                    string json = await HttpClient.Get("https://api.filmix.tv/api-fx/post/171042/video-links", timeoutSeconds: 8, headers: HeadersModel.Init("Authorization", $"Bearer {init.token_apitv}"));
                    hash = Regex.Match(json?.Replace("\\", ""), "/s/([^/]+)/").Groups[1].Value;
                }
                else if (!string.IsNullOrEmpty(init.token))
                {
                    string json = await HttpClient.Get($"{init.corsHost()}/api/v2/post/171042?user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_token={init.token}&user_dev_vendor=Xiaomi", timeoutSeconds: 8);
                    hash = Regex.Match(json?.Replace("\\", ""), "/s/([^/]+)/").Groups[1].Value;
                }
                else
                {
                    return null;
                }

                if (init.livehash && !string.IsNullOrWhiteSpace(hash))
                    memoryCache.Set(memKey, hash, DateTime.Now.AddHours(2));
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

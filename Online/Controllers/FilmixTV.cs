using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using Shared.Engine.Online;
using Online;
using Shared.Engine.CORE;
using System;
using Shared.Model.Online.Filmix;
using Shared.Model.Online;
using System.Text;
using Shared.Model.Online.FilmixTV;
using System.Text.RegularExpressions;

namespace Lampac.Controllers.LITE
{
    /// <summary>
    /// Автор https://github.com/fellicienne
    /// https://github.com/immisterio/Lampac/pull/41
    /// </summary>
    public class FilmixTV : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/filmixtv")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, int postid, int t = -1, int? s = null, bool origsource = false, bool rjson = false, bool similar = false)
        {
            var init = await loadKit(AppInit.conf.FilmixTV, (j, i, c) =>
            {
                if (j.ContainsKey("pro"))
                    i.pro = c.pro;
                i.user_apitv = c.user_apitv;
                i.passwd_apitv = c.passwd_apitv;
                return i;
            });

            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.user_apitv))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            #region accessToken
            if (string.IsNullOrEmpty(init.token_apitv))
            {
                if (System.IO.File.Exists("cache/filmixtv.hash"))
                    init.hash_apitv = System.IO.File.ReadAllText("cache/filmixtv.hash");

                var auth = await InvokeCache<string>($"filmixtv:accessToken:{init.user_apitv}:{init.passwd_apitv}", TimeSpan.FromHours(2), proxyManager, async res =>
                {
                    if (init.hash_apitv == null)
                    {
                        var rtk = await HttpClient.Get<JObject>($"{init.corsHost()}/api-fx/request-token", timeoutSeconds: 8);
                        if (rtk == null || !rtk.ContainsKey("token"))
                            return res.Fail("rtk");

                        string hash = rtk.Value<string>("token");
                        if (string.IsNullOrEmpty(hash))
                            return res.Fail("hash");

                        init.hash_apitv = hash;
                        System.IO.File.WriteAllText("cache/filmixtv.hash", hash);
                    }

                    JObject root_auth = null;

                    if (System.IO.File.Exists("cache/filmixtv.auth"))
                    {
                        string authFile = System.IO.File.ReadAllText("cache/filmixtv.auth");
                        string refreshToken = Regex.Match(authFile, "\"refreshToken\": ?\"([^\"]+)\"").Groups[1].Value;
                        root_auth = await HttpClient.Get<JObject>($"{init.corsHost()}/api-fx/refresh?refreshToken={refreshToken}", timeoutSeconds: 8, headers: HeadersModel.Init("hash", init.hash_apitv));
                    }
                    else
                    {
                        var data = new System.Net.Http.StringContent($"{{\"user_name\":\"{init.user_apitv}\",\"user_passw\":\"{init.passwd_apitv}\",\"session\":true}}", Encoding.UTF8, "application/json");
                        root_auth = await HttpClient.Post<JObject>($"{init.corsHost()}/api-fx/auth", data, timeoutSeconds: 8, headers: HeadersModel.Init("hash", init.hash_apitv));
                    }

                    string accessToken = root_auth?.GetValue("accessToken")?.ToString();
                    if (string.IsNullOrEmpty(accessToken))
                        return res.Fail("accessToken");

                    System.IO.File.WriteAllText("cache/filmixtv.auth", root_auth.ToString());
                    return accessToken;
                });

                if (!auth.IsSuccess)
                    return OnError(auth.ErrorMsg);

                init.token_apitv = auth.Value;
            }
            #endregion

            var headers = httpHeaders(init, HeadersModel.Init
            (
                ("Authorization", $"Bearer {init.token_apitv}"),
                ("hash", init.hash_apitv)
            ));

            var oninvk = new FilmixTVInvoke
            (
               host,
               init.corsHost(),
               ongettourl => HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: headers),
               (url, data) => HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, headers: headers),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy),
               requesterror: () => proxyManager.Refresh(),
               rjson: rjson
            );

            if (postid == 0)
            {
                var search = await InvokeCache<SearchResult>($"filmixtv:search:{title}:{original_title}:{clarification}:{similar}", cacheTime(40, init: init), proxyManager, async res =>
                {
                    return await oninvk.Search(title, original_title, clarification, year, similar);
                });

                if (!search.IsSuccess)
                    return OnError(search.ErrorMsg);

                if (search.Value.id == 0)
                    return ContentTo(rjson ? search.Value.similars.ToJson() : search.Value.similars.ToHtml());

                postid = search.Value.id;
            }

            var cache = await InvokeCache<RootObject>($"filmixtv:post:{postid}:{init.token_apitv}", cacheTime(20, init: init), proxyManager, onget: async res =>
            {
                string json = await HttpClient.Get($"{init.corsHost()}/api-fx/post/{postid}/video-links", timeoutSeconds: 8, headers: headers);

                return oninvk.Post(json);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, init.pro, postid, title, original_title, t, s, vast: init.vast), origsource: origsource);
        }
    }
}

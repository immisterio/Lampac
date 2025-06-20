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
using F = System.IO.File;
using System.Web;

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
        async public ValueTask<ActionResult> Index(string title, string original_title, int clarification, int year, int postid, int t = -1, int? s = null, bool origsource = false, bool rjson = false, bool similar = false)
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
                return OnError("user_apitv", gbcache: false);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            #region accessToken
            string hashFile = $"cache/filmixtv-{CrypTo.md5(init.user_apitv)}.hash";

            if (F.Exists(hashFile))
            {
                init.hash_apitv = F.ReadAllText(hashFile);
            }
            else
            {
                var rtk = await HttpClient.Get<JObject>($"{init.corsHost()}/api-fx/request-token");
                if (rtk == null || !rtk.ContainsKey("token"))
                    return ShowError("rtk");

                string hash = rtk.Value<string>("token");
                if (string.IsNullOrEmpty(hash))
                    return ShowError("hash");

                init.hash_apitv = hash;
            }

            var auth = await InvokeCache<string>($"filmixtv:accessToken:{init.user_apitv}:{init.passwd_apitv}:{init.hash_apitv}", TimeSpan.FromHours(2), proxyManager, async res =>
            {
                JObject root_auth = null;

                string authFile = $"cache/filmixtv-{CrypTo.md5(init.hash_apitv)}.auth";

                if (F.Exists(authFile))
                {
                    string refreshToken = Regex.Match(F.ReadAllText(authFile), "\"refreshToken\": ?\"([^\"]+)\"").Groups[1].Value;
                    root_auth = await HttpClient.Get<JObject>($"{init.corsHost()}/api-fx/refresh?refreshToken={HttpUtility.UrlEncode(refreshToken)}", headers: HeadersModel.Init("hash", init.hash_apitv));
                }
                else
                {
                    var data = new System.Net.Http.StringContent($"{{\"user_name\":\"{init.user_apitv}\",\"user_passw\":\"{init.passwd_apitv}\",\"session\":true}}", Encoding.UTF8, "application/json");
                    root_auth = await HttpClient.Post<JObject>($"{init.corsHost()}/api-fx/auth", data, headers: HeadersModel.Init("hash", init.hash_apitv));
                }

                string accessToken = root_auth?.GetValue("accessToken")?.ToString();
                if (string.IsNullOrEmpty(accessToken))
                {
                    if (root_auth != null)
                    {
                        if (root_auth.ContainsKey("msg"))
                            return res.Fail(root_auth.Value<string>("msg"));

                        return res.Fail(root_auth.ToString());
                    }

                    return res.Fail("accessToken");
                }

                F.WriteAllText(hashFile, init.hash_apitv);
                F.WriteAllText(authFile, root_auth.ToString());
                return accessToken;
            });

            if (!auth.IsSuccess)
                return ShowError(HttpUtility.HtmlEncode(auth.ErrorMsg));

            init.token_apitv = auth.Value;
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
                    return ContentTo(rjson ? search.Value.similars.Value.ToJson() : search.Value.similars.Value.ToHtml());

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

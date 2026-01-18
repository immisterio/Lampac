using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.FilmixTV;
using Shared.Models.Online.Settings;
using System.Text;
using System.Threading;
using F = System.IO.File;

namespace Online.Controllers
{
    /// <summary>
    /// Автор https://github.com/fellicienne
    /// https://github.com/immisterio/Lampac/pull/41
    /// </summary>
    public class FilmixTV : BaseOnlineController<FilmixSettings>
    {
        public FilmixTV() : base(AppInit.conf.FilmixTV) 
        {
            loadKitInitialization = (j, i, c) =>
            {
                if (j.ContainsKey("pro"))
                    i.pro = c.pro;

                i.user_apitv = c.user_apitv;
                i.passwd_apitv = c.passwd_apitv;
                return i;
            };
        }

        static readonly SemaphoreSlim _accessTokenLock = new SemaphoreSlim(1, 1);

        [HttpGet]
        [Route("lite/filmixtv")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, int postid, int t = -1, int? s = null, bool rjson = false, bool similar = false, string source = null, string id = null)
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

            if (string.IsNullOrEmpty(init.user_apitv))
                return OnError("user_apitv", statusCode: 401, gbcache: false);

            #region accessToken
            var tokenResult = await EnsureAccessToken();

            if (!tokenResult.IsSuccess)
                return ShowError(HttpUtility.HtmlEncode(tokenResult.ErrorMsg));

            init.token_apitv = tokenResult.Token;
            #endregion

            var bearer = HeadersModel.Init
            (
                ("Authorization", $"Bearer {init.token_apitv}"),
                ("hash", init.hash_apitv)
            );

            var oninvk = new FilmixTVInvoke
            (
               host,
               init.corsHost(),
               bearer,
               httpHydra,
               streamfile => HostStreamProxy(streamfile),
               requesterror: () => proxyManager?.Refresh(),
               rjson: rjson
            );

            if (postid == 0)
            {
                var search = await InvokeCacheResult($"filmixtv:search:{title}:{original_title}:{clarification}:{similar}", 40, 
                    () => oninvk.Search(title, original_title, clarification, year, similar)
                );

                if (!search.IsSuccess)
                    return OnError(search.ErrorMsg);

                if (search.Value.id == 0)
                    return await ContentTpl(search.Value.similars);

                postid = search.Value.id;
            }

            rhubFallback:
            var cache = await InvokeCacheResult<RootObject>($"filmixtv:post:{postid}:{init.token_apitv}", 20, async e =>
            {
                string json = await httpHydra.Get($"{init.corsHost()}/api-fx/post/{postid}/video-links", addheaders: bearer, safety: true);
                if (json == null)
                    return e.Fail("json", refresh_proxy: true);

                return e.Success(oninvk.Post(json));
            });

            if (IsRhubFallback(cache, safety: true))
                goto rhubFallback;

            return await ContentTpl(cache, () => oninvk.Tpl(cache.Value, init.pro, postid, title, original_title, t, s, vast: init.vast));
        }


        #region [Copilot AI] EnsureAccessToken
        async ValueTask<(bool IsSuccess, string Token, string ErrorMsg)> EnsureAccessToken()
        {
            try
            {
                await _accessTokenLock.WaitAsync(TimeSpan.FromMinutes(1));

                string hashFile = $"cache/filmixtv-{CrypTo.md5(init.user_apitv)}.hash";

                if (F.Exists(hashFile))
                {
                    init.hash_apitv = F.ReadAllText(hashFile);
                }
                else
                {
                    var rtk = await Http.Get<JObject>($"{init.corsHost()}/api-fx/request-token", 
                        proxy: proxy, httpversion: init.httpversion, timeoutSeconds: 30
                    );

                    if (rtk == null || !rtk.ContainsKey("token"))
                        return (false, null, "rtk");

                    string hash = rtk.Value<string>("token");
                    if (string.IsNullOrEmpty(hash))
                        return (false, null, "hash");

                    init.hash_apitv = hash;
                }

                var auth = await InvokeCacheResult<string>($"filmixtv:accessToken:{init.user_apitv}:{init.passwd_apitv}:{init.hash_apitv}", 60*5, async e =>
                {
                    JObject root_auth = null;

                    string authFile = $"cache/filmixtv-{CrypTo.md5(init.hash_apitv)}.auth";

                    if (F.Exists(authFile))
                    {
                        string refreshToken = Regex.Match(F.ReadAllText(authFile), "\"refreshToken\": ?\"([^\"]+)\"").Groups[1].Value;

                        root_auth = await Http.Get<JObject>($"{init.corsHost()}/api-fx/refresh?refreshToken={HttpUtility.UrlEncode(refreshToken)}", 
                            proxy: proxy, headers: HeadersModel.Init("hash", init.hash_apitv), httpversion: init.httpversion, timeoutSeconds: 30
                        );
                    }
                    else
                    {
                        var data = new System.Net.Http.StringContent($"{{\"user_name\":\"{init.user_apitv}\",\"user_passw\":\"{init.passwd_apitv}\",\"session\":true}}", Encoding.UTF8, "application/json");
                        root_auth = await Http.Post<JObject>($"{init.corsHost()}/api-fx/auth", data, 
                            proxy: proxy, headers: HeadersModel.Init("hash", init.hash_apitv), httpversion: init.httpversion, timeoutSeconds: 30
                        );
                    }

                    string accessToken = root_auth?.GetValue("accessToken")?.ToString();
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        if (root_auth != null)
                        {
                            if (root_auth.ContainsKey("msg"))
                                return e.Fail(root_auth.Value<string>("msg"));

                            return e.Fail(root_auth.ToString());
                        }

                        return e.Fail("accessToken");
                    }

                    F.WriteAllText(hashFile, init.hash_apitv);
                    F.WriteAllText(authFile, root_auth.ToString());
                    return e.Success(accessToken);
                });

                if (!auth.IsSuccess)
                    return (false, null, auth.ErrorMsg);

                return (true, auth.Value, null);
            }
            catch (Exception ex) { return (false, null, ex.Message); }
            finally
            {
                _accessTokenLock.Release();
            }
        }
        #endregion
    }
}

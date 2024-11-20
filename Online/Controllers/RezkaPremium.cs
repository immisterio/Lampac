using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using Online;
using Shared.Engine.Online;
using Shared.Model.Online.Rezka;
using System;
using Shared.Model.Online;
using System.Collections.Generic;
using Lampac.Models.LITE;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using System.Net;
using System.Management;
using System.Linq;

namespace Lampac.Controllers.LITE
{
    public class RezkaPremium : BaseOnlineController
    {
        #region InitRezkaInvoke
        static string uid = null, typeuid = null;

        async public ValueTask<RezkaInvoke> InitRezkaInvoke()
        {
            var init = AppInit.conf.RezkaPrem;

            var rch = new RchClient(HttpContext, host, init.rhub);
            var proxyManager = new ProxyManager("rhsprem", init);
            var proxy = proxyManager.Get();

            #region uid
            if (uid == null)
            {
                try
                {
                    uid = System.IO.File.ReadAllText("/etc/machine-id");
                    typeuid = "machine-id";
                }
                catch
                {
                    if (AppInit.Win32NT)
                    {
                        try
                        {
                            using (var searcher = new ManagementObjectSearcher("select ProcessorId from Win32_Processor"))
                            {
                                foreach (var item in searcher.Get())
                                    uid = item["ProcessorId"].ToString();
                            }

                            typeuid = "ProcessorId";
                        }
                        catch { }
                    }
                }

                if (string.IsNullOrEmpty(uid))
                {
                    if (System.IO.File.Exists("cache/uid"))
                    {
                        uid = System.IO.File.ReadAllText("cache/uid");
                    }
                    else
                    {
                        uid = CrypTo.SHA256(DateTime.Now.ToBinary().ToString());
                        System.IO.File.WriteAllText("cache/uid", uid);
                    }

                    typeuid = "generate";
                }
            }
            #endregion

            string cookie = await getCookie(init, proxy);
            if (string.IsNullOrEmpty(cookie))
                return null;

            string user_id = Regex.Match(cookie, "dle_user_id=([0-9]+)", RegexOptions.IgnoreCase).Groups[1].Value;

            var headers = httpHeaders(init, HeadersModel.Init(
               ("X-Lampac-App", "1"),
               ("X-Lampac-Version", $"{appversion}.{minorversion}"),
               ("X-Lampac-Device-Id", $"lampac:user_id/{user_id}:{(AppInit.Win32NT ? "win32" : "linux")}:uid/{Regex.Replace(uid, "[^a-zA-Z0-9]+", "").Trim()}:type_uid/{typeuid}"),
               ("X-Lampac-Cookie", cookie),
               ("User-Agent", HttpContext.Request.Headers.UserAgent)
            ));

            var rheaders = headers.ToDictionary(k => k.name, v => v.val);

            string country = GeoIP2.Country(HttpContext.Connection.RemoteIpAddress.ToString());

            if (!init.rhub && country != null)
                headers.Add(new HeadersModel("X-Real-IP", HttpContext.Connection.RemoteIpAddress.ToString()));

            if (init.forceua)
                country = "UA";

            return new RezkaInvoke
            (
                host,
                "kwwsv=22odps1df",
                init.scheme,
                MaybeInHls(init.hls, init),
                true,
                ongettourl => init.rhub ? rch.Get(ongettourl, rheaders) : HttpClient.Get(ongettourl, timeoutSeconds: 8, proxy: proxy, headers: headers),
                (url, data) => init.rhub ? rch.Post(url, data, rheaders) : HttpClient.Post(url, data, timeoutSeconds: 8, proxy: proxy, headers: headers),
                streamfile => HostStreamProxy(init, RezkaInvoke.fixcdn(country, init.uacdn, streamfile), proxy: proxy, plugin: "rhsprem"),
                requesterror: () => { if (init.rhub == false) proxyManager.Refresh(); }
            );
        }
        #endregion

        #region RezkaBind
        [HttpGet]
        [Route("/lite/rhs/bind")]
        async public Task<ActionResult> RezkaBind(string login, string pass)
        {
            string html = string.Empty;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(pass))
            {
                html = "Введите данные аккаунта rezka.ag <br> <br><form method=\"get\" action=\"/lite/rhs/bind\"><input type=\"text\" name=\"login\" placeholder=\"email\"> &nbsp; &nbsp; <input type=\"text\" name=\"pass\" placeholder=\"пароль\"><br><br><button>Авторизоваться</button></form> ";
            }
            else
            {
                string cookie = await getCookie(new RezkaSettings(AppInit.conf.RezkaPrem.host) 
                {
                    login = login,
                    passwd = pass
                });

                if (string.IsNullOrEmpty(cookie))
                {
                    html = "Ошибка авторизации ;(";
                }
                else
                {
                    html = "Добавьте в init.conf<br><br>\"RezkaPrem\": {<br>&nbsp;&nbsp;\"enable\": true,<br>&nbsp;&nbsp;\"cookie\": \"" + cookie + "\"<br>}";
                }
            }

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("lite/rhsprem")]
        async public Task<ActionResult> Index(string account_email, long kinopoisk_id, string imdb_id, string title, string original_title, int clarification, int year, int s = -1, string href = null, bool rjson = false)
        {
            var init = AppInit.conf.RezkaPrem;
            if (!init.enable || init.rip)
                return OnError("disabled");

            if (init.rhub)
            {
                if (!AppInit.conf.rch.enable)
                    return ShowError(RchClient.ErrorMsg);

                if (string.IsNullOrEmpty(init.cookie))
                    return ShowError("rhub работает через cookie - IP:9118/lite/rhs/bind");
            }

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
                return OnError("href/title = null");

            var oninvk = await InitRezkaInvoke();
            if (oninvk == null)
                return OnError("authorization error ;(");

            var proxyManager = new ProxyManager("rhsprem", init);
            var rch = new RchClient(HttpContext, host, init.rhub);

            var cache = await InvokeCache<EmbedModel>($"rhsprem:{kinopoisk_id}:{imdb_id}:{title}:{original_title}:{year}:{clarification}:{href}", cacheTime(10, init: init), null, async res => 
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(kinopoisk_id, imdb_id, title, original_title, clarification, year, href);
            });

            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg ?? "content = null", proxyManager, weblog: oninvk.requestlog);

            return OnResult(cache, () => oninvk.Html(cache.Value, account_email, kinopoisk_id, imdb_id, title, original_title, clarification, year, s, href, true, rjson).Replace("/rezka", "/rhsprem"));
        }


        #region Serial
        [HttpGet]
        [Route("lite/rhsprem/serial")]
        async public Task<ActionResult> Serial(string account_email, long kinopoisk_id, string imdb_id, string title, string original_title, int clarification,int year, string href, long id, int t, int s = -1, bool rjson = false)
        {
            var init = AppInit.conf.RezkaPrem;
            if (!init.enable || init.rip)
                return OnError("disabled");

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
                return OnError("href/title = null");

            var oninvk = await InitRezkaInvoke();
            if (oninvk == null)
                return OnError("authorization error ;(");

            var rch = new RchClient(HttpContext, host, init.rhub);

            var cache_root = await InvokeCache<Episodes>($"rhsprem:view:serial:{id}:{t}", cacheTime(20, init: init), null, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.SerialEmbed(id, t);
            });

            if (!cache_root.IsSuccess)
                return OnError(cache_root.ErrorMsg ?? "root = null", weblog: oninvk.requestlog);

            var cache_content = await InvokeCache<EmbedModel>($"rhsprem:{kinopoisk_id}:{imdb_id}:{title}:{original_title}:{year}:{clarification}:{href}", cacheTime(10, init: init), null, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(kinopoisk_id, imdb_id, title, original_title, clarification, year, href);
            });

            if (!cache_content.IsSuccess)
                return OnError(cache_content.ErrorMsg ?? "content = null", weblog: oninvk.requestlog);

            return ContentTo(oninvk.Serial(cache_root.Value, cache_content.Value, account_email, kinopoisk_id, imdb_id, title, original_title, clarification, year, href, id, t, s, true, rjson).Replace("/rezka", "/rhsprem"));
        }
        #endregion

        #region Movie
        [HttpGet]
        [Route("lite/rhsprem/movie")]
        [Route("lite/rhsprem/movie.m3u8")]
        async public Task<ActionResult> Movie(string title, string original_title, long id, int t, int director = 0, int s = -1, int e = -1, string favs = null, bool play = false)
        {
            var init = AppInit.conf.RezkaPrem;
            if (!init.enable || init.rip)
                return OnError("disabled");

            var oninvk = await InitRezkaInvoke();
            if (oninvk == null)
                return OnError("authorization error ;(");

            var proxyManager = new ProxyManager("rhsprem", init);
            var rch = new RchClient(HttpContext, host, init.rhub);

            var cache = await InvokeCache<MovieModel>($"rhsprem:view:get_cdn_series:{id}:{t}:{director}:{s}:{e}", cacheTime(5, mikrotik: 1, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Movie(id, t, director, s, e, favs);
            });

            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg ?? "md == null", weblog: oninvk.requestlog);

            string result = oninvk.Movie(cache.Value, title, original_title, play);
            if (result == null)
                return OnError("result = null", weblog: oninvk.requestlog);

            if (play)
                return Redirect(result);

            return Content(result.Replace("/rezka", "/rhsprem"), "application/json; charset=utf-8");
        }
        #endregion


        #region getCookie
        static string authCookie = null;

        async ValueTask<string> getCookie(RezkaSettings init, WebProxy proxy = null)
        {
            if (authCookie != null)
                return authCookie;

            if (!string.IsNullOrEmpty(init.cookie))
                return $"dle_user_taken=1; {Regex.Match(init.cookie, "(dle_user_id=[^;]+;)")} {Regex.Match(init.cookie, "(dle_password=[^;]+)")}".Trim();

            if (string.IsNullOrEmpty(init.login) || string.IsNullOrEmpty(init.passwd))
                return null;

            if (memoryCache.TryGetValue("rhsprem:login", out _))
                return null;

            memoryCache.Set("rhsprem:login", 0, TimeSpan.FromMinutes(1));

            try
            {
                var clientHandler = new System.Net.Http.HttpClientHandler()
                {
                    AllowAutoRedirect = false
                };

                if (proxy != null)
                {
                    clientHandler.UseProxy = true;
                    clientHandler.Proxy = proxy;
                }

                clientHandler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
                using (var client = new System.Net.Http.HttpClient(clientHandler))
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    client.DefaultRequestHeaders.Add("user-agent", HttpClient.UserAgent);

                    var postParams = new Dictionary<string, string>
                    {
                        { "login_name", init.login },
                        { "login_password", init.passwd },
                        { "login_not_save", "0" }
                    };

                    using (var postContent = new System.Net.Http.FormUrlEncodedContent(postParams))
                    {
                        using (var response = await client.PostAsync($"{init.host}/ajax/login/", postContent))
                        {
                            if (response.Headers.TryGetValues("Set-Cookie", out var cook))
                            {
                                string cookie = string.Empty;

                                foreach (string line in cook)
                                {
                                    if (string.IsNullOrEmpty(line))
                                        continue;

                                    if (line.Contains("=deleted;"))
                                        continue;

                                    if (line.Contains("dle_user_id") || line.Contains("dle_password"))
                                        cookie += $"{line.Split(";")[0]}; ";
                                }

                                if (cookie.Contains("dle_user_id") && cookie.Contains("dle_password"))
                                    authCookie = $"dle_user_taken=1; {Regex.Replace(cookie.Trim(), ";$", "")}";
                            }
                        }
                    }
                }
            }
            catch { }

            return authCookie;
        }
        #endregion
    }
}

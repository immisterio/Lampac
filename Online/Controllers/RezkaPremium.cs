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

namespace Lampac.Controllers.LITE
{
    public class RezkaPremium : BaseOnlineController
    {
        #region InitRezkaInvoke
        static string uid = null, typeuid = null;

        List<HeadersModel> apiHeaders(RezkaSettings init, string cookie)
        {
            return httpHeaders(init, HeadersModel.Init(
               ("X-Lampac-App", "1"),
               ("X-Lampac-Version", $"{appversion}.{minorversion}"),
               ("X-Lampac-Device-Id", $"{(AppInit.Win32NT ? "win32" : "linux")}:uid/{Regex.Replace(uid, "[^a-zA-Z0-9]+", "").Trim()}:type_uid/{typeuid}"),
               ("X-Lampac-Cookie", cookie),
               ("User-Agent", requestInfo.UserAgent)
            ));
        }

        async public ValueTask<(RezkaInvoke invk, string log)> InitRezkaInvoke(RezkaSettings init)
        {
            init.host = new RezkaSettings(null, "kwwsv=22odps1df").host;

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            var proxyManager = new ProxyManager(init);
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

            var cook = await getCookie(init, proxy);
            if (string.IsNullOrEmpty(cook.cookie))
                return (null, cook.log);

            var headers = apiHeaders(init, cook.cookie);

            string country = requestInfo.Country;

            if (!rch.enable && country != null)
                headers.Add(new HeadersModel("X-Real-IP", requestInfo.IP));

            if (init.forceua)
                country = "UA";

            return (new RezkaInvoke
            (
                host,
                init.host,
                init.scheme,
                init.hls,
                true,
                (url, _) => rch.enable ? rch.Get(url, headers) : HttpClient.Get(url, timeoutSeconds: 8, proxy: proxy, headers: headers, statusCodeOK: !url.Contains("do=search")),
                (url, data, _) => rch.enable ? rch.Post(url, data, headers) : HttpClient.Post(url, data, timeoutSeconds: 8, proxy: proxy, headers: headers),
                streamfile => HostStreamProxy(init, RezkaInvoke.fixcdn(country, init.uacdn, streamfile), proxy: proxy),
                requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
            ), null);
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
                string cookie = (await getCookie(new RezkaSettings(null, "kwwsv=22odps1df") 
                {
                    login = login,
                    passwd = pass
                })).cookie;

                if (string.IsNullOrEmpty(cookie))
                {
                    html = "Ошибка авторизации ;(";
                }
                else
                {
                    html = "Добавьте в init.conf<br><br>\"RezkaPrem\": {<br>&nbsp;&nbsp;\"enable\": true,<br>&nbsp;&nbsp;\"cookie\": \"" + cookie + "\"<br>},\"Rezka\": {<br>&nbsp;&nbsp;\"enable\": false<br>}";
                }
            }

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("lite/rhsprem")]
        async public Task<ActionResult> Index(long kinopoisk_id, string imdb_id, string title, string original_title, int clarification, int year, int s = -1, string href = null, bool rjson = false, int serial = -1)
        {
            var init = await loadKit(AppInit.conf.RezkaPrem);
            if (await IsBadInitialization(init))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: serial == 0 ? null : -1);

            if (rch.enable)
            {
                if (!AppInit.conf.rch.enable)
                    return ShowError(RchClient.ErrorMsg);

                if (string.IsNullOrEmpty(init.cookie))
                    return ShowError("rhub работает через cookie - IP:9118/lite/rhs/bind");
            }

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
                return OnError("href/title = null");

            var onrezka = await InitRezkaInvoke(init);
            if (onrezka.invk == null)
                return OnError("authorization error ;(", weblog: onrezka.log);

            var oninvk = onrezka.invk;

            var cache = await InvokeCache<EmbedModel>($"rhsprem:{kinopoisk_id}:{imdb_id}:{title}:{original_title}:{year}:{clarification}:{href}", cacheTime(10, init: init), rch.enable ? null : proxyManager, async res => 
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(kinopoisk_id, imdb_id, title, original_title, clarification, year, href);
            });

            if (cache.IsSuccess && cache.Value.IsEmpty && cache.Value.content != null)
                return ShowError(cache.Value.content);

            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg ?? "content = null", proxyManager, weblog: oninvk.requestlog, refresh_proxy: !rch.enable);

            return OnResult(cache, () => oninvk.Html(cache.Value, accsArgs(string.Empty), kinopoisk_id, imdb_id, title, original_title, clarification, year, s, href, true, rjson).Replace("/rezka", "/rhsprem"), gbcache: !rch.enable);
        }


        #region Serial
        [HttpGet]
        [Route("lite/rhsprem/serial")]
        async public Task<ActionResult> Serial(long kinopoisk_id, string imdb_id, string title, string original_title, int clarification,int year, string href, long id, int t, int s = -1, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.RezkaPrem);
            if (await IsBadInitialization(init))
                return badInitMsg;

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
                return OnError("href/title = null");

            var onrezka = await InitRezkaInvoke(init);
            if (onrezka.invk == null)
                return OnError("authorization error ;(", weblog: onrezka.log);

            var oninvk = onrezka.invk;

            var proxyManager = new ProxyManager(init);
            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);

            var cache_root = await InvokeCache<Episodes>($"rhsprem:view:serial:{id}:{t}", cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.SerialEmbed(id, t);
            });

            if (!cache_root.IsSuccess)
                return OnError(cache_root.ErrorMsg ?? "root = null", weblog: oninvk.requestlog);

            var cache_content = await InvokeCache<EmbedModel>($"rhsprem:{kinopoisk_id}:{imdb_id}:{title}:{original_title}:{year}:{clarification}:{href}", cacheTime(10, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(kinopoisk_id, imdb_id, title, original_title, clarification, year, href);
            });

            if (!cache_content.IsSuccess)
                return OnError(cache_content.ErrorMsg ?? "content = null", weblog: oninvk.requestlog);

            return ContentTo(oninvk.Serial(cache_root.Value, cache_content.Value, accsArgs(string.Empty), kinopoisk_id, imdb_id, title, original_title, clarification, year, href, id, t, s, true, rjson).Replace("/rezka", "/rhsprem"));
        }
        #endregion

        #region Movie
        [HttpGet]
        [Route("lite/rhsprem/movie")]
        [Route("lite/rhsprem/movie.m3u8")]
        async public Task<ActionResult> Movie(string title, string original_title, long id, int t, int director = 0, int s = -1, int e = -1, string favs = null, bool play = false)
        {
            var init = await loadKit(AppInit.conf.RezkaPrem);
            if (await IsBadInitialization(init))
                return badInitMsg;

            var onrezka = await InitRezkaInvoke(init);
            if (onrezka.invk == null)
                return OnError("authorization error ;(", weblog: onrezka.log);

            var oninvk = onrezka.invk;

            var proxyManager = new ProxyManager(init);
            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: s == -1 ? null : -1);

            var cache = await InvokeCache<MovieModel>($"rhsprem:view:get_cdn_series:{id}:{t}:{director}:{s}:{e}:{init.cookie}", cacheTime(5, mikrotik: 1, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Movie(id, t, director, s, e, favs);
            });

            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg ?? "md == null", weblog: oninvk.requestlog);

            string result = oninvk.Movie(cache.Value, title, original_title, play, vast: init.vast);
            if (result == null)
                return OnError("result = null", weblog: oninvk.requestlog);

            if (play)
                return Redirect(result);

            return ContentTo(result.Replace("/rezka", "/rhsprem"));
        }
        #endregion


        #region getCookie
        static string authCookie = null;

        async ValueTask<(string cookie, string log)> getCookie(RezkaSettings init, WebProxy proxy = null)
        {
            if (authCookie != null)
                return (authCookie, null);

            if (!string.IsNullOrEmpty(init.cookie))
                return ($"dle_user_taken=1; {Regex.Match(init.cookie, "(dle_user_id=[^;]+;)")} {Regex.Match(init.cookie, "(dle_password=[^;]+)")}".Trim(), null);

            if (string.IsNullOrEmpty(init.login) || string.IsNullOrEmpty(init.passwd))
                return default;

            if (memoryCache.TryGetValue("rhsprem:login", out _))
                return default;

            string loglines = string.Empty;
            memoryCache.Set("rhsprem:login", 0, TimeSpan.FromSeconds(20));

            try
            {
                var clientHandler = new System.Net.Http.HttpClientHandler()
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate
                };

                if (proxy != null)
                {
                    clientHandler.UseProxy = true;
                    clientHandler.Proxy = proxy;
                }

                clientHandler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
                using (var client = new System.Net.Http.HttpClient(clientHandler))
                {
                    client.Timeout = TimeSpan.FromSeconds(15);

                    foreach (var item in apiHeaders(init, string.Empty))
                        client.DefaultRequestHeaders.Add(item.name, item.val);

                    var postParams = new Dictionary<string, string>
                    {
                        { "login_name", init.login },
                        { "login_password", init.passwd },
                        { "login_not_save", "0" }
                    };

                    using (var postContent = new System.Net.Http.FormUrlEncodedContent(postParams))
                    {
                        loglines += $"POST: {init.host}/ajax/login/\n";
                        loglines += $"{postContent.ReadAsStringAsync().Result}\n";

                        using (var response = await client.PostAsync($"{init.host}/ajax/login/", postContent))
                        {
                            loglines += $"\n\nStatusCode: {(int)response.StatusCode}\n";

                            if (response.Headers.TryGetValues("Set-Cookie", out var cook))
                            {
                                string cookie = string.Empty;

                                foreach (string line in cook)
                                {
                                    if (string.IsNullOrEmpty(line))
                                        continue;

                                    if (line.Contains("=deleted;"))
                                        continue;

                                    loglines += $"Set-Cookie: {line}\n";

                                    if (line.Contains("dle_user_id") || line.Contains("dle_password"))
                                        cookie += $"{line.Split(";")[0]}; ";
                                }

                                if (cookie.Contains("dle_user_id") && cookie.Contains("dle_password"))
                                    authCookie = $"dle_user_taken=1; {Regex.Replace(cookie.Trim(), ";$", "")}";

                                loglines += $"authCookie: {authCookie}\n\n";
                                loglines += await response.Content.ReadAsStringAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { loglines += $"\n\nException: {ex}"; }

            return (authCookie, loglines);
        }
        #endregion
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared.Models.Online.Rezka;
using Shared.Models.Online.Settings;
using System.Management;
using System.Net;

namespace Online.Controllers
{
    public class RezkaPremium : BaseOnlineController
    {
        #region InitRezkaInvoke
        static string uid = null, typeuid = null;

        static void genUid()
        {
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
        }

        List<HeadersModel> apiHeaders(RezkaSettings init, string cookie)
        {
            genUid();
            return httpHeaders(init, HeadersModel.Init(
               ("X-Lampac-App", "1"),
               ("X-Lampac-Version", $"{appversion}.{minorversion}"),
               ("X-Lampac-Device-Id", $"{(AppInit.Win32NT ? "win32" : "linux")}:uid/{Regex.Replace(uid, "[^a-zA-Z0-9]+", "").Trim()}:type_uid/{typeuid}"),
               ("X-Lampac-Cookie", cookie),
               ("User-Agent", requestInfo.UserAgent)
            ));
        }

        async public ValueTask<(RezkaInvoke invk, string cookie, string log)> InitRezkaInvoke(RezkaSettings init)
        {
            init.host = new RezkaSettings(null, "kwwsv=22odps1df").host;

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            genUid();

            var cook = await getCookie(init, proxy);
            if (string.IsNullOrEmpty(cook.cookie))
                return (null, null, cook.log);

            var headers = apiHeaders(init, cook.cookie);

            string country = requestInfo.Country;

            if (!rch.enable && country != null)
                headers.Add(new HeadersModel("X-Real-IP", requestInfo.IP));

            if (init.forceua)
                country = "UA";

            init.premium = true;

            return (new RezkaInvoke
            (
                host,
                init,
                (url, _) => rch.enable ? rch.Get(url, headers) : Http.Get(url, timeoutSeconds: 8, proxy: proxy, headers: headers, statusCodeOK: !url.Contains("do=search")),
                (url, data, _) => rch.enable ? rch.Post(url, data, headers) : Http.Post(url, data, timeoutSeconds: 8, proxy: proxy, headers: headers),
                streamfile => HostStreamProxy(init, RezkaInvoke.fixcdn(country, init.uacdn, streamfile), proxy: proxy),
                requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
            ), cook.cookie, null);
        }
        #endregion

        #region RezkaBind
        [HttpGet]
        [AllowAnonymous]
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
                var result = await getCookie(new RezkaSettings(null, "kwwsv=22odps1df") 
                {
                    login = login,
                    passwd = pass
                }, timeoutError: 5);

                if (string.IsNullOrEmpty(result.cookie))
                {
                    html = "Ошибка авторизации ;(<br><br>" + result.log.Replace("\n", "<br>");
                }
                else
                {
                    html = "Добавьте в init.conf<br><br>\"RezkaPrem\": {<br>&nbsp;&nbsp;\"enable\": true,<br>&nbsp;&nbsp;\"cookie\": \"" + result.cookie + "\"<br>},<br>\"Rezka\": {<br>&nbsp;&nbsp;\"enable\": false<br>}";
                }
            }

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        #region Initialization
        ValueTask<RezkaSettings> Initialization()
        {
            return loadKit(AppInit.conf.RezkaPrem, (j, i, c) =>
            {
                if (j.ContainsKey("uacdn"))
                    i.uacdn = c.uacdn;

                if (j.ContainsKey("forceua"))
                    i.forceua = c.forceua;

                if (j.ContainsKey("reserve"))
                    i.reserve = c.reserve;

                return i;
            });
        }
        #endregion

        [HttpGet]
        [Route("lite/rhsprem")]
        async public ValueTask<ActionResult> Index(string title, string original_title, int clarification, int year, int s = -1, string href = null, bool rjson = false, int serial = -1, bool similar = false, string source = null, string id = null)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.ToLower() is "rezka" or "hdrezka")
                    href = id;
            }

            var proxyManager = new ProxyManager(init);
            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: serial == 0 ? null : -1);

            if (rch.enable)
            {
                if (!AppInit.conf.rch.enable)
                    return ShowError(RchClient.ErrorMsg);

                if (string.IsNullOrEmpty(init.cookie))
                    return ShowError($"rhub работает через cookie - {host}/lite/rhs/bind");
            }

            if (string.IsNullOrWhiteSpace(href) && string.IsNullOrWhiteSpace(title))
                return OnError("href/title = null");

            var onrezka = await InitRezkaInvoke(init);
            if (onrezka.invk == null)
                return OnError("authorization error ;(", weblog: onrezka.log);

            var oninvk = onrezka.invk;

            #region search
            string search_uri = null;

            if (string.IsNullOrEmpty(href))
            {
                var search = await InvokeCache<SearchModel>($"rhsprem:search:{title}:{original_title}:{clarification}:{year}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    var content = await oninvk.Search(title, original_title, clarification, year);
                    if (content == null || (content.IsEmpty && content.content != null))
                        return res.Fail(content.content ?? "content");

                    return content;
                });

                if (search.ErrorMsg != null && search.ErrorMsg.Contains("Ошибка доступа"))
                    return ShowError(search.ErrorMsg);

                if (similar || string.IsNullOrEmpty(search.Value?.href))
                {
                    if (search.Value?.IsEmpty == true)
                        return ShowError(search.Value.content ?? "поиск не дал результатов");

                    return OnResult(search, () =>
                    {
                        if (search.Value.similar == null)
                            return string.Empty;

                        var stpl = new SimilarTpl(search.Value.similar.Count);

                        foreach (var similar in search.Value.similar)
                        {
                            string link = $"{host}/lite/rhsprem?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&href={HttpUtility.UrlEncode(similar.href)}";

                            stpl.Append(similar.title, similar.year, string.Empty, link, PosterApi.Size(similar.img));
                        }

                        return rjson ? stpl.ToJson() : stpl.ToHtml();
                    });
                }

                href = search.Value.href;
                search_uri = search.Value.search_uri;
            }
            #endregion

            var cache = await InvokeCache<EmbedModel>($"rhsprem:{href}", cacheTime(10, init: init), rch.enable ? null : proxyManager, async res => 
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(href, search_uri);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, accsArgs(string.Empty), title, original_title, s, href, true, rjson).Replace("/rezka", "/rhsprem"), gbcache: !rch.enable);
        }


        #region Serial
        [HttpGet]
        [Route("lite/rhsprem/serial")]
        async public ValueTask<ActionResult> Serial(string title, string original_title, string href, long id, int t, int s = -1, bool rjson = false, bool similar = false)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrWhiteSpace(href))
                return OnError("href = null");

            var onrezka = await InitRezkaInvoke(init);
            if (onrezka.invk == null)
                return OnError("authorization error ;(", weblog: onrezka.log);

            var oninvk = onrezka.invk;

            var proxyManager = new ProxyManager(init);
            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            var cache_root = await InvokeCache<Episodes>($"rhsprem:view:serial:{id}:{t}", cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
            {
                return await oninvk.SerialEmbed(id, t);
            });

            var cache_content = await InvokeCache<EmbedModel>($"rhsprem:{href}", cacheTime(10, init: init), rch.enable ? null : proxyManager, async res =>
            {
                return await oninvk.Embed(href, null);
            });

            return ContentTo(oninvk.Serial(cache_root.Value, cache_content.Value, accsArgs(string.Empty), title, original_title, href, id, t, s, true, rjson).Replace("/rezka", "/rhsprem"));
        }
        #endregion

        #region Movie
        [HttpGet]
        [Route("lite/rhsprem/movie")]
        [Route("lite/rhsprem/movie.m3u8")]
        async public ValueTask<ActionResult> Movie(string title, string original_title, long id, int t, int director = 0, int s = -1, int e = -1, string favs = null, bool play = false)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var onrezka = await InitRezkaInvoke(init);
            if (onrezka.invk == null)
                return OnError("authorization error ;(", weblog: onrezka.log);

            var oninvk = onrezka.invk;

            var proxyManager = new ProxyManager(init);
            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: s == -1 ? null : -1);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            var cache = await InvokeCache<MovieModel>($"rhsprem:view:get_cdn_series:{id}:{t}:{director}:{s}:{e}:{onrezka.cookie}", cacheTime(5, mikrotik: 1, init: init), rch.enable ? null : proxyManager, async res =>
            {
                return await oninvk.Movie(id, t, director, s, e, favs);
            });

            string result = oninvk.Movie(cache.Value, title, original_title, play, vast: init.vast);
            if (result == null)
                return OnError("result = null", weblog: oninvk.requestlog);

            if (play)
                return RedirectToPlay(result);

            return ContentTo(result.Replace("/rezka", "/rhsprem"));
        }
        #endregion


        #region getCookie
        static Dictionary<string, string> authCookie = new Dictionary<string, string>();

        async ValueTask<(string cookie, string log)> getCookie(RezkaSettings init, WebProxy proxy = null, int timeoutError = 15)
        {
            if (!string.IsNullOrEmpty(init.cookie))
                return ($"dle_user_taken=1; {Regex.Match(init.cookie, "(dle_user_id=[^;]+;)")} {Regex.Match(init.cookie, "(dle_password=[^;]+)")}".Trim(), null);

            if (string.IsNullOrEmpty(init.login) || string.IsNullOrEmpty(init.passwd))
                return default;

            if (authCookie.TryGetValue(init.login, out string _cook))
                return (_cook, null);

            if (memoryCache.TryGetValue($"rhsprem:login:{init.login}", out _))
                return default;

            string loglines = string.Empty;
            memoryCache.Set($"rhsprem:login:{init.login}", 0, TimeSpan.FromSeconds(timeoutError));

            try
            {
                using (var clientHandler = new System.Net.Http.HttpClientHandler()
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate
                })
                {

                    if (proxy != null)
                    {
                        clientHandler.UseProxy = true;
                        clientHandler.Proxy = proxy;
                    }
                    else { clientHandler.UseProxy = false; }

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
                                    {
                                        _cook = $"dle_user_taken=1; {Regex.Replace(cookie.Trim(), ";$", "")}";
                                        authCookie.TryAdd(init.login, _cook);
                                    }

                                    loglines += $"authCookie: {authCookie}\n\n";
                                    loglines += await response.Content.ReadAsStringAsync();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { loglines += $"\n\nHDRezka Exception: {ex}"; }

            return (_cook, loglines);
        }
        #endregion
    }
}

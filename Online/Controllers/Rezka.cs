using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared.Models.Online.Rezka;
using Shared.Models.Online.Settings;
using System.Net;

namespace Online.Controllers
{
    public class Rezka : BaseOnlineController
    {
        #region InitRezkaInvoke
        async public ValueTask<RezkaInvoke> InitRezkaInvoke(RezkaSettings init)
        {
            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            string country = init.forceua ? "UA" : requestInfo.Country;
            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);

            var headers = httpHeaders(init);
            var cookie = await getCookie(init);

            if (rch.enable && cookie != null)
                headers.Add(new HeadersModel("Cookie", rhubCookie));

            if (init.xapp)
                headers.Add(new HeadersModel("X-App-Hdrezka-App", "1"));

            if (init.xrealip)
                headers.Add(new HeadersModel("realip", requestInfo.IP));

            return new RezkaInvoke
            (
                host,
                init,
                (url, hed) =>
                    rch.enable ? rch.Get(url, HeadersModel.Join(hed, headers)) :
                    Http.Get(init.cors(url), timeoutSeconds: 8, proxy: proxy, headers: HeadersModel.Join(hed, headers), cookieContainer: cookieContainer, statusCodeOK: false),
                (url, data, hed) =>
                    rch.enable ? rch.Post(url, data, HeadersModel.Join(hed, headers)) :
                    Http.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, headers: HeadersModel.Join(hed, headers), cookieContainer: cookieContainer),
                streamfile => HostStreamProxy(init, RezkaInvoke.fixcdn(country, init.uacdn, streamfile), proxy: proxy, headers: RezkaInvoke.StreamProxyHeaders(init)),
                requesterror: () => proxyManager.Refresh()
            );
        }
        #endregion

        #region Initialization
        ValueTask<RezkaSettings> Initialization()
        {
            return loadKit(AppInit.conf.Rezka, (j, i, c) =>
            {
                if (j.ContainsKey("premium"))
                    i.premium = c.premium;

                if (j.ContainsKey("uacdn"))
                    i.uacdn = c.uacdn;

                if (j.ContainsKey("forceua"))
                    i.forceua = c.forceua;

                if (j.ContainsKey("reserve"))
                    i.reserve = c.reserve;

                if (j.ContainsKey("ajax"))
                    i.ajax = c.ajax;

                return i;
            });
        }
        #endregion

        [HttpGet]
        [Route("lite/rezka")]
        async public ValueTask<ActionResult> Index(string title, string original_title, int clarification, int year, int s = -1, string href = null, bool rjson = false, int serial = -1, bool similar = false, string source = null, string id = null)
        {
            #region Initialization
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (init.premium || AppInit.conf.RezkaPrem.enable) 
                return ShowError("Замените Rezka на RezkaPrem в init.conf");

            if (string.IsNullOrWhiteSpace(href) && string.IsNullOrWhiteSpace(title))
                return OnError();

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: serial == 0 ? null : -1);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            if (rch.enable)
            {
                if (rch.IsNotSupport("web", out string rch_error))
                    return ShowError($"Нужен HDRezka Premium<br>{init.host}/payments/");

                if (requestInfo.Country == "RU")
                {
                    if (rch.InfoConnected()?.rchtype != "apk")
                        return ShowError($"Нужен HDRezka Premium<br>{init.host}/payments/");

                    if (await getCookie(init) == null)
                        return ShowError("Укажите логин/пароль или cookie");
                }
            }
            #endregion

            if (string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.ToLower() is "rezka" or "hdrezka")
                    href = id;
            }

            var oninvk = await InitRezkaInvoke(init);
            var proxyManager = new ProxyManager(init);

            #region search
            string search_uri = null;

            if (string.IsNullOrEmpty(href))
            {
                var search = await InvokeCache<SearchModel>($"rezka:search:{title}:{original_title}:{clarification}:{year}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
                {
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
                            string link = $"{host}/lite/rezka?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&href={HttpUtility.UrlEncode(similar.href)}";

                            stpl.Append(similar.title, similar.year, string.Empty, link, PosterApi.Size(similar.img));
                        }

                        return rjson ? stpl.ToJson() : stpl.ToHtml();
                    });
                }

                href = search.Value.href;
                search_uri = search.Value.search_uri;
            }
            #endregion

            var cache = await InvokeCache<EmbedModel>($"rezka:{href}", cacheTime(10, init: init), rch.enable ? null : proxyManager, async res =>
            {
                return await oninvk.Embed(href, search_uri);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, accsArgs(string.Empty), title, original_title, s, href, true, rjson), gbcache: !rch.enable);
        }


        #region Serial
        [HttpGet]
        [Route("lite/rezka/serial")]
        async public ValueTask<ActionResult> Serial(string title, string original_title, string href, long id, int t, int s = -1, bool rjson = false)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(href))
                return OnError();

            var oninvk = await InitRezkaInvoke(init);
            var proxyManager = new ProxyManager(init);

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            Episodes root = await InvokeCache($"rezka:view:serial:{id}:{t}", cacheTime(20, init: init), () => oninvk.SerialEmbed(id, t));
            if (root == null)
                return OnError(null, gbcache: !rch.enable);

            var content = await InvokeCache($"rezka:{href}", cacheTime(20, init: init), () => oninvk.Embed(href, null));
            if (content == null)
                return OnError(null, gbcache: !rch.enable);

            return ContentTo(oninvk.Serial(root, content, accsArgs(string.Empty), title, original_title, href, id, t, s, true, rjson));
        }
        #endregion

        #region Movie
        [HttpGet]
        [Route("lite/rezka/movie")]
        [Route("lite/rezka/movie.m3u8")]
        async public ValueTask<ActionResult> Movie(string title, string original_title, string voice, long id, int t, int director = 0, int s = -1, int e = -1, string favs = null, bool play = false)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var oninvk = await InitRezkaInvoke(init);
            var proxyManager = new ProxyManager(init);

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: s == -1 ? null : -1);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            string realip = (init.xrealip && init.corseu) ? requestInfo.IP : "";

            MovieModel md = null;

            /// ajax = true (get_cdn_series)
            /// ajax = false (movie | get_cdn_series)
            /// ajax = null (movie)

            if (init.ajax != null && init.ajax.Value == false && !string.IsNullOrEmpty(voice))
            {
                md = await InvokeCache(rch.ipkey($"rezka:movie:{voice}:{realip}:{init.cookie}", proxyManager), cacheTime(20, mikrotik: 1, init: init), () => oninvk.Movie(voice), proxyManager);
            }

            if (md == null && init.ajax != null)
                md = await InvokeCache(rch.ipkey($"rezka:view:get_cdn_series:{id}:{t}:{director}:{s}:{e}:{realip}:{init.cookie}", proxyManager), cacheTime(20, mikrotik: 1, init: init), () => oninvk.Movie(id, t, director, s, e, favs), proxyManager);

            if (md == null)
                return OnError(null, gbcache: !rch.enable);

            string result = oninvk.Movie(md, title, original_title, play, vast: init.vast);
            if (result == null)
                return OnError(null, gbcache: !rch.enable);

            if (play)
                return RedirectToPlay(result);

            return ContentTo(result);
        }
        #endregion


        #region getCookie
        static string rhubCookie = string.Empty;
        static CookieContainer cookieContainer = null;

        async ValueTask<CookieContainer> getCookie(RezkaSettings init)
        {
            if (cookieContainer != null)
                return cookieContainer;

            string domain = Regex.Match(init.host, "https?://([^/]+)").Groups[1].Value;

            #region setCookieContainer
            void setCookieContainer(string coks)
            {
                cookieContainer = new CookieContainer();

                if (coks != string.Empty && !coks.Contains("hdmbbs"))
                    coks = $"hdmbbs=1; {coks}";

                if (!coks.Contains("dle_user_taken"))
                    coks = $"dle_user_taken=1; {coks}";

                foreach (string line in coks.Split(";"))
                {
                    if (string.IsNullOrEmpty(line) || !line.Contains("="))
                        continue;

                    var g = Regex.Match(line.Trim(), "^([^=]+)=([^\n\r]+)").Groups;
                    string name = g[1].Value.Trim();
                    string value = g[2].Value.Trim();

                    if (name is "CLID" or "MUID" or "_clck" or "_clsk")
                        continue;

                    if (name.StartsWith("_ym_"))
                        continue;

                    if (name != "PHPSESSID")
                        rhubCookie += $"{name}={value}; ";

                    if (name == "hdmbbs")
                    {
                        cookieContainer.Add(new Cookie()
                        {
                            Path = "/",
                            Expires = DateTime.Today.AddYears(1),
                            Domain = domain,
                            Name = name,
                            Value = value
                        });
                    }
                    else
                    {
                        cookieContainer.Add(new Cookie()
                        {
                            Path = "/",
                            Expires = name == "PHPSESSID" ? default : DateTime.Today.AddYears(1),
                            Domain = $".{domain}",
                            Name = name,
                            Value = value,
                            HttpOnly = true
                        });
                    }
                }

                rhubCookie = Regex.Replace(rhubCookie.Trim(), ";$", "");
            }
            #endregion

            if (!string.IsNullOrEmpty(init.cookie))
            {
                setCookieContainer(init.cookie.Trim());
                return cookieContainer;
            }

            if (string.IsNullOrEmpty(init.login) || string.IsNullOrEmpty(init.passwd))
            {
                setCookieContainer(string.Empty);
                return cookieContainer;
            }

            if (memoryCache.TryGetValue("rezka:login", out _))
                return null;

            memoryCache.Set("rezka:login", 0, TimeSpan.FromMinutes(2));

            try
            {
                using (var clientHandler = new System.Net.Http.HttpClientHandler()
                {
                    AllowAutoRedirect = false
                })
                {
                    clientHandler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
                    using (var client = new System.Net.Http.HttpClient(clientHandler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(20);
                        client.DefaultRequestHeaders.Add("user-agent", Http.UserAgent);

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

                                        if (line.Contains("=deleted;") || !line.Contains(domain))
                                            continue;

                                        string c = line.Split(";")[0];
                                        if (c.Contains("="))
                                        {
                                            string name = c.Split("=")[0];
                                            if (cookie.Contains(name))
                                            {
                                                cookie = Regex.Replace(cookie, $"{name}=[^;]+", $"{name}={c.Split("=")[1]}");
                                            }
                                            else
                                            {
                                                cookie += $"{c}; ";
                                            }
                                        }
                                    }

                                    if (cookie.Contains("dle_user_id") && cookie.Contains("dle_password"))
                                    {
                                        setCookieContainer(cookie.Trim());
                                        return cookieContainer;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }
        #endregion
    }
}

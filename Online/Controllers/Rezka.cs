using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared.Models.Online.Rezka;
using Shared.Models.Online.Settings;
using System.Net;

namespace Online.Controllers
{
    public class Rezka : BaseOnlineController<RezkaSettings>
    {
        RezkaInvoke oninvk;

        public Rezka() : base(AppInit.conf.Rezka) 
        {
            loadKitInitialization = (j, i, c) =>
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
            };

            requestInitializationAsync = async () =>
            {
                string country = init.forceua ? "UA" : requestInfo.Country;

                var headers = httpHeaders(init);
                var cookie = await getCookie();

                if (rch?.enable == true && cookie != null)
                    headers.Add(new HeadersModel("Cookie", rhubCookie));

                if (init.xapp)
                    headers.Add(new HeadersModel("X-App-Hdrezka-App", "1"));

                if (init.xrealip)
                    headers.Add(new HeadersModel("realip", requestInfo.IP));

                oninvk = new RezkaInvoke
                (
                    host,
                    "lite/rezka",
                    init,
                    (url, hed) =>
                        rch?.enable == true 
                            ? rch.Get(url, HeadersModel.Join(hed, headers)) 
                            : Http.Get(init.cors(url), timeoutSeconds: 8, proxy: proxy, headers: HeadersModel.Join(hed, headers), cookieContainer: cookieContainer, statusCodeOK: false),
                    (url, data, hed) =>
                        rch?.enable == true 
                            ? rch.Post(url, data, HeadersModel.Join(hed, headers)) 
                            : Http.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, headers: HeadersModel.Join(hed, headers), cookieContainer: cookieContainer),
                    streamfile => HostStreamProxy(RezkaInvoke.fixcdn(country, init.uacdn, streamfile), headers: RezkaInvoke.StreamProxyHeaders(init)),
                    requesterror: () => proxyManager?.Refresh()
                );
            };
        }


        [HttpGet]
        [Route("lite/rezka")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, int s = -1, string href = null, bool rjson = false, int serial = -1, bool similar = false, string source = null, string id = null)
        {
            if (await IsRequestBlocked(rch: true, rch_check: false))
                return badInitMsg;

            #region Initialization
            if (init.premium || AppInit.conf.RezkaPrem.enable) 
                return ShowError("Замените Rezka на RezkaPrem в init.conf");

            if (string.IsNullOrWhiteSpace(href) && string.IsNullOrWhiteSpace(title))
                return OnError();

            if (rch != null)
            {
                if (rch.IsNotConnected() || rch.IsRequiredConnected())
                    return ContentTo(rch.connectionMsg);

                if (rch.enable)
                {
                    if (rch.IsNotSupportRchAccess("web", out string rch_error))
                        return ShowError($"Нужен HDRezka Premium<br>{init.host}/payments/");

                    if (requestInfo.Country == "RU")
                    {
                        if (rch.InfoConnected()?.rchtype != "apk")
                            return ShowError($"Нужен HDRezka Premium<br>{init.host}/payments/");

                        if (await getCookie() == null)
                            return ShowError("Укажите логин/пароль или cookie");
                    }
                }
            }
            #endregion

            if (string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.ToLower() is "rezka" or "hdrezka")
                    href = id;
            }

            #region search
            string search_uri = null;

            if (string.IsNullOrEmpty(href))
            {
                var search = await InvokeCacheResult<SearchModel>($"rezka:search:{title}:{original_title}:{clarification}:{year}", 40, async e =>
                {
                    var content = await oninvk.Search(title, original_title, clarification, year);
                    if (content == null || (content.IsEmpty && content.content != null))
                        return e.Fail(content.content ?? "content");

                    return e.Success(content);
                });

                if (search.ErrorMsg != null && search.ErrorMsg.Contains("Ошибка доступа"))
                    return ShowError(search.ErrorMsg);

                if (similar || string.IsNullOrEmpty(search.Value?.href))
                {
                    if (search.Value?.IsEmpty == true)
                        return ShowError(search.Value.content ?? "поиск не дал результатов");

                    return await ContentTpl(search, () =>
                    {
                        if (search.Value.similar == null)
                            return default;

                        var stpl = new SimilarTpl(search.Value.similar.Count);

                        foreach (var similar in search.Value.similar)
                        {
                            string link = $"{host}/lite/rezka?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&href={HttpUtility.UrlEncode(similar.href)}";

                            stpl.Append(similar.title, similar.year, string.Empty, link, PosterApi.Size(similar.img));
                        }

                        return stpl;
                    });
                }

                href = search.Value.href;
                search_uri = search.Value.search_uri;
            }
            #endregion

            var cache = await InvokeCacheResult($"rezka:{href}", 10, 
                () => oninvk.Embed(href, search_uri)
            );

            return await ContentTpl(cache, () => oninvk.Tpl(cache.Value, accsArgs(string.Empty), title, original_title, s, href, true, rjson));
        }


        #region Serial
        [HttpGet]
        [Route("lite/rezka/serial")]
        async public Task<ActionResult> Serial(string title, string original_title, string href, long id, int t, int s = -1, bool rjson = false)
        {
            if (string.IsNullOrEmpty(href))
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            Episodes root = await InvokeCache($"rezka:view:serial:{id}:{t}", 20, 
                () => oninvk.SerialEmbed(id, t)
            );

            if (root == null)
                return OnError();

            var content = await InvokeCache($"rezka:{href}", 20, 
                () => oninvk.Embed(href, null)
            );

            if (content == null)
                return OnError();

            return await ContentTpl(oninvk.Serial(root, content, accsArgs(string.Empty), title, original_title, href, id, t, s, true, rjson));
        }
        #endregion

        #region Movie
        [HttpGet]
        [Route("lite/rezka/movie")]
        [Route("lite/rezka/movie.m3u8")]
        async public ValueTask<ActionResult> Movie(string title, string original_title, string voice, long id, int t, int director = 0, int s = -1, int e = -1, string favs = null, bool play = false)
        {
            if (await IsRequestBlocked(rch: true, rch_check: false))
                return badInitMsg;

            if (rch != null)
            {
                if (rch.IsNotConnected())
                {
                    if (init.rhub_fallback && play)
                        rch.Disabled();
                    else
                        return ContentTo(rch.connectionMsg);
                }

                if (!play && rch.IsRequiredConnected())
                    return ContentTo(rch.connectionMsg);
            }

            string realip = (init.xrealip && init.corseu) ? requestInfo.IP : "";

            MovieModel md = null;

            /// ajax = true (get_cdn_series)
            /// ajax = false (movie | get_cdn_series)
            /// ajax = null (movie)

            if (init.ajax != null && init.ajax.Value == false && !string.IsNullOrEmpty(voice))
            {
                md = await InvokeCache(ipkey($"rezka:movie:{voice}:{realip}:{init.cookie}"), 20, 
                    () => oninvk.Movie(voice)
                );
            }

            if (md == null && init.ajax != null)
            {
                md = await InvokeCache(ipkey($"rezka:view:get_cdn_series:{id}:{t}:{director}:{s}:{e}:{realip}:{init.cookie}"), 20,
                    () => oninvk.Movie(id, t, director, s, e, favs)
                );
            }

            if (md == null)
                return OnError();

            string result = oninvk.Movie(md, title, original_title, play, vast: init.vast);
            if (result == null)
                return OnError();

            if (play)
                return RedirectToPlay(result);

            return ContentTo(result);
        }
        #endregion


        #region getCookie
        static string rhubCookie = string.Empty;
        static CookieContainer cookieContainer = null;

        async ValueTask<CookieContainer> getCookie()
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

                        foreach (var h in Http.defaultFullHeaders)
                            client.DefaultRequestHeaders.TryAddWithoutValidation(h.Key, h.Value);

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

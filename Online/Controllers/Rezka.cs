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

namespace Lampac.Controllers.LITE
{
    public class Rezka : BaseOnlineController
    {
        #region InitRezkaInvoke
        async public ValueTask<RezkaInvoke> InitRezkaInvoke()
        {
            var init = AppInit.conf.Rezka;

            var proxyManager = new ProxyManager("rezka", init);
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
                init.corsHost(),
                init.scheme,
                MaybeInHls(init.hls, init),
                init.premium,
                (url, hed) => rch.enable ? rch.Get(url, HeadersModel.Join(hed, headers)) : 
                                           HttpClient.Get(init.cors(url), timeoutSeconds: 8, proxy: proxy, headers: HeadersModel.Join(hed, headers), cookieContainer: cookieContainer, statusCodeOK: false),
                (url, data, hed) => rch.enable ? rch.Post(url, data, HeadersModel.Join(hed, headers)) : 
                                                 HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, headers: HeadersModel.Join(hed, headers), cookieContainer: cookieContainer),
                streamfile => HostStreamProxy(init, RezkaInvoke.fixcdn(country, init.uacdn, streamfile), proxy: proxy, plugin: "rezka", headers: RezkaInvoke.StreamProxyHeaders(init.host)),
                requesterror: () => proxyManager.Refresh()
            );
        }
        #endregion

        [HttpGet]
        [Route("lite/rezka")]
        async public Task<ActionResult> Index(long kinopoisk_id, string imdb_id, string title, string original_title, int clarification, int year, int s = -1, string href = null, bool rjson = false, int serial = -1)
        {
            var init = AppInit.conf.Rezka.Clone();
            if (IsBadInitialization(init, out ActionResult action, rch: true))
                return action;

            if (init.premium || AppInit.conf.RezkaPrem.enable) 
                return ShowError("Используйте RezkaPremium в init.conf вместо Rezka");

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
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
                    if (rch.InfoConnected().rchtype != "apk")
                        return ShowError($"Нужен HDRezka Premium<br>{init.host}/payments/");

                    if (await getCookie(init) == null)
                        return ShowError("Укажите логин/пароль или cookie");
                }
            }

            var oninvk = await InitRezkaInvoke();
            var proxyManager = new ProxyManager("rezka", init);

            string memKey = $"rezka:{kinopoisk_id}:{imdb_id}:{title}:{original_title}:{year}:{clarification}:{href}";
            if (!hybridCache.TryGetValue(memKey, out EmbedModel content))
            {
                content = await oninvk.Embed(kinopoisk_id, imdb_id, title, original_title, clarification, year, href);
                if (content == null)
                    return OnError(null, gbcache: !rch.enable);

                proxyManager.Success();
                hybridCache.Set(memKey, content, cacheTime(20, init: init));
            }

            if (content.IsEmpty && content.content != null)
                return ShowError(content.content);

            return ContentTo(oninvk.Html(content, accsArgs(string.Empty), kinopoisk_id, imdb_id, title, original_title, clarification, year, s, href, true, rjson));
        }


        #region Serial
        [HttpGet]
        [Route("lite/rezka/serial")]
        async public Task<ActionResult> Serial(long kinopoisk_id, string imdb_id, string title, string original_title, int clarification,int year, string href, long id, int t, int s = -1, bool rjson = false)
        {
            var init = AppInit.conf.Rezka.Clone();
            if (IsBadInitialization(init, out ActionResult action))
                return action;

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
                return OnError();

            var oninvk = await InitRezkaInvoke();
            var proxyManager = new ProxyManager("rezka", init);

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            Episodes root = await InvokeCache($"rezka:view:serial:{id}:{t}", cacheTime(20, init: init), () => oninvk.SerialEmbed(id, t));
            if (root == null)
                return OnError(null, gbcache: !rch.enable);

            var content = await InvokeCache($"rezka:{kinopoisk_id}:{imdb_id}:{title}:{original_title}:{year}:{clarification}:{href}", cacheTime(20, init: init), () => oninvk.Embed(kinopoisk_id, imdb_id, title, original_title, clarification, year, href));
            if (content == null)
                return OnError(null, gbcache: !rch.enable);

            return ContentTo(oninvk.Serial(root, content, accsArgs(string.Empty), kinopoisk_id, imdb_id, title, original_title, clarification, year, href, id, t, s, true, rjson));
        }
        #endregion

        #region Movie
        [HttpGet]
        [Route("lite/rezka/movie")]
        [Route("lite/rezka/movie.m3u8")]
        async public Task<ActionResult> Movie(string title, string original_title, long id, int t, int director = 0, int s = -1, int e = -1, string favs = null, bool play = false)
        {
            var init = AppInit.conf.Rezka.Clone();
            if (IsBadInitialization(init, out ActionResult action))
                return action;

            var oninvk = await InitRezkaInvoke();
            var proxyManager = new ProxyManager("rezka", init);

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: s == -1 ? null : -1);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            string realip = (init.xrealip && init.corseu) ? requestInfo.IP : "";

            var md = await InvokeCache(rch.ipkey($"rezka:view:get_cdn_series:{id}:{t}:{director}:{s}:{e}:{realip}", proxyManager), cacheTime(20, mikrotik: 1, init: init), () => oninvk.Movie(id, t, director, s, e, favs), proxyManager);
            if (md == null)
                return OnError(null, gbcache: !rch.enable);

            string result = oninvk.Movie(md, title, original_title, play, vast: init.vast);
            if (result == null)
                return OnError(null, gbcache: !rch.enable);

            if (play)
                return Redirect(result);

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
                var clientHandler = new System.Net.Http.HttpClientHandler()
                {
                    AllowAutoRedirect = false
                };

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
            catch { }

            return null;
        }
        #endregion
    }
}

﻿using System.Threading.Tasks;
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

            var headers = httpHeaders(init, HeadersModel.Init(
                ("Origin", init.host),
                ("Referer", init.host + "/")
            ));

            string cookie = await getCookie(init);

            if (!string.IsNullOrEmpty(cookie))
                headers.Add(new HeadersModel("Cookie", cookie));

            if (init.xapp)
                headers.Add(new HeadersModel("X-App-Hdrezka-App", "1"));

            if (init.xrealip)
                headers.Add(new HeadersModel("realip", requestInfo.IP));

            string country = init.forceua ? "UA" : requestInfo.Country;
            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);

            return new RezkaInvoke
            (
                host,
                init.corsHost(),
                init.scheme,
                MaybeInHls(init.hls, init),
                authCookie != null || !string.IsNullOrEmpty(init.cookie),
                ongettourl => rch.enable ? rch.Get(ongettourl, headers) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: headers, statusCodeOK: false),
                (url, data) => rch.enable ? rch.Post(url, data, headers) : HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, headers: headers),
                streamfile => HostStreamProxy(init, RezkaInvoke.fixcdn(country, init.uacdn, streamfile), proxy: proxy, plugin: "rezka"),
                requesterror: () => proxyManager.Refresh()
            );
        }
        #endregion

        [HttpGet]
        [Route("lite/rezka")]
        async public Task<ActionResult> Index(long kinopoisk_id, string imdb_id, string title, string original_title, int clarification, int year, int s = -1, string href = null, bool rjson = false, int serial = -1)
        {
            var init = AppInit.conf.Rezka;
            if (!init.enable)
                return OnError();

            if (init.rhub && !AppInit.conf.rch.enable)
                return ShowError(RchClient.ErrorMsg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
                return OnError();

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: serial == 0 ? null : -1);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            if (!init.premium && rch.enable)
            {
                if (rch.IsNotSupport("web", out string rch_error))
                    return ShowError($"Нужен HDRezka Premium<br>{init.host}/payments/");

                if (requestInfo.Country == "RU")
                {
                    if (rch.InfoConnected().rchtype != "apk")
                        return ShowError($"Нужен HDRezka Premium<br>{init.host}/payments/");

                    if (string.IsNullOrEmpty(await getCookie(init)))
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

                if (content.IsEmpty && content.content != null)
                    return ShowError(content.content);

                proxyManager.Success();
                hybridCache.Set(memKey, content, cacheTime(20, init: init));
            }

            return ContentTo(oninvk.Html(content, accsArgs(string.Empty), kinopoisk_id, imdb_id, title, original_title, clarification, year, s, href, true, rjson));
        }


        #region Serial
        [HttpGet]
        [Route("lite/rezka/serial")]
        async public Task<ActionResult> Serial(long kinopoisk_id, string imdb_id, string title, string original_title, int clarification,int year, string href, long id, int t, int s = -1, bool rjson = false)
        {
            var init = AppInit.conf.Rezka;
            if (!init.enable)
                return OnError();

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

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
            var init = AppInit.conf.Rezka;
            if (!init.enable)
                return OnError();

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

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
        static string authCookie = null;

        async ValueTask<string> getCookie(RezkaSettings init)
        {
            if (authCookie != null)
                return authCookie;

            if (!string.IsNullOrEmpty(init.cookie))
                return $"dle_user_taken=1; {Regex.Match(init.cookie, "(dle_user_id=[^;]+;)")} {Regex.Match(init.cookie, "(dle_password=[^;]+)")}".Trim();

            if (string.IsNullOrEmpty(init.login) || string.IsNullOrEmpty(init.passwd))
                return null;

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

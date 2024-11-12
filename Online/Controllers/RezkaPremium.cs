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

        async public ValueTask<RezkaInvoke> InitRezkaInvoke()
        {
            var init = AppInit.conf.RezkaPrem;

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
               ("Cookie", cookie),
               ("User-Agent", HttpContext.Request.Headers.UserAgent)
            ));

            string country = GeoIP2.Country(HttpContext.Connection.RemoteIpAddress.ToString());

            if (country != null)
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
                ongettourl => HttpClient.Get(ongettourl, timeoutSeconds: 8, proxy: proxy, headers: headers),
                (url, data) => HttpClient.Post(url, data, timeoutSeconds: 8, proxy: proxy, headers: headers),
                streamfile => HostStreamProxy(init, RezkaInvoke.fixcdn(country, init.uacdn, streamfile), proxy: proxy, plugin: "rhsprem"),
                requesterror: () => proxyManager.Refresh()
            );
        }
        #endregion

        [HttpGet]
        [Route("lite/rhsprem")]
        async public Task<ActionResult> Index(long kinopoisk_id, string imdb_id, string title, string original_title, int clarification, int year, int s = -1, string href = null, bool rjson = false)
        {
            var init = AppInit.conf.RezkaPrem;
            if (!init.enable || init.rip)
                return OnError("disabled");

            if (init.rhub)
                return ShowError(RchClient.ErrorMsg);

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
                return OnError("href/title = null");

            var oninvk = await InitRezkaInvoke();
            if (oninvk == null)
                return OnError("authorization error ;(");

            var proxyManager = new ProxyManager("rhsprem", init);

            var content = await InvokeCache($"rhsprem:{kinopoisk_id}:{imdb_id}:{title}:{original_title}:{year}:{clarification}:{href}", cacheTime(10, init: init), () => oninvk.Embed(kinopoisk_id, imdb_id, title, original_title, clarification, year, href));
            if (content == null)
                return OnError("content = null", proxyManager, weblog: oninvk.requestlog);

            return ContentTo(oninvk.Html(content, kinopoisk_id, imdb_id, title, original_title, clarification, year, s, href, true, rjson).Replace("/rezka", "/rhsprem"));
        }


        #region Serial
        [HttpGet]
        [Route("lite/rhsprem/serial")]
        async public Task<ActionResult> Serial(long kinopoisk_id, string imdb_id, string title, string original_title, int clarification,int year, string href, long id, int t, int s = -1, bool rjson = false)
        {
            var init = AppInit.conf.RezkaPrem;
            if (!init.enable || init.rip)
                return OnError("disabled");

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
                return OnError("href/title = null");

            var oninvk = await InitRezkaInvoke();
            if (oninvk == null)
                return OnError("authorization error ;(");

            Episodes root = await InvokeCache($"rhsprem:view:serial:{id}:{t}", cacheTime(20, init: init), () => oninvk.SerialEmbed(id, t));
            if (root == null)
                return OnError("root = null", weblog: oninvk.requestlog);

            var content = await InvokeCache($"rhsprem:{kinopoisk_id}:{imdb_id}:{title}:{original_title}:{year}:{clarification}:{href}", cacheTime(10, init: init), () => oninvk.Embed(kinopoisk_id, imdb_id, title, original_title, clarification, year, href));
            if (content == null)
                return OnError("content = null", weblog: oninvk.requestlog);

            return ContentTo(oninvk.Serial(root, content, kinopoisk_id, imdb_id, title, original_title, clarification, year, href, id, t, s, true, rjson).Replace("/rezka", "/rhsprem"));
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

            var md = await InvokeCache($"rhsprem:view:get_cdn_series:{id}:{t}:{director}:{s}:{e}", cacheTime(5, mikrotik: 1, init: init), () => oninvk.Movie(id, t, director, s, e, favs), proxyManager);
            if (md == null)
                return OnError("md == null", weblog: oninvk.requestlog);

            string result = oninvk.Movie(md, title, original_title, play);
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
                return $"dle_user_taken=1; {Regex.Match(init.cookie, "(dle_user_id=[^;]+;)")} {Regex.Match(init.cookie, "(dle_password=[^;]+;)")}";

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

                                    if (line.Contains("dle_user_taken") || line.Contains("dle_user_id") || line.Contains("dle_password"))
                                        cookie += $"{line.Split(";")[0]}; ";
                                }

                                if (cookie.Contains("dle_user_id") && cookie.Contains("dle_password"))
                                    authCookie = cookie.Trim();
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

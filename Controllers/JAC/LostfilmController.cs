using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Engine.Parse;
using Lampac.Engine;
using Lampac.Models.JAC;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;

namespace Lampac.Controllers.CRON
{
    [Route("lostfilm/[action]")]
    public class LostfilmController : BaseController
    {
        #region TakeLogin
        async static Task<string> TakeLogin()
        {
            string authKey = "lostfilm:TakeLogin()";
            if (Startup.memoryCache.TryGetValue(authKey, out _))
                return null;

            Startup.memoryCache.Set(authKey, 0, AppInit.conf.multiaccess ? TimeSpan.FromMinutes(2) : TimeSpan.FromSeconds(20));

            try
            {
                var clientHandler = new System.Net.Http.HttpClientHandler()
                {
                    AllowAutoRedirect = false
                };

                clientHandler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
                using (var client = new System.Net.Http.HttpClient(clientHandler))
                {
                    client.Timeout = TimeSpan.FromSeconds(AppInit.conf.jac.timeoutSeconds);
                    client.MaxResponseContentBufferSize = 2000000; // 2MB
                    client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/75.0.3770.100 Safari/537.36");

                    var postParams = new Dictionary<string, string>
                    {
                        { "act", "users" },
                        { "type", "login" },
                        { "mail", AppInit.conf.Lostfilm.login.u },
                        { "pass", AppInit.conf.Lostfilm.login.p },
                        { "need_captcha", "" },
                        { "captcha", "" },
                        { "rem", "1" }
                    };

                    using (var postContent = new System.Net.Http.FormUrlEncodedContent(postParams))
                    {
                        using (var response = await client.PostAsync($"{AppInit.conf.Lostfilm.host}/ajaxik.users.php", postContent))
                        {
                            if (response.Headers.TryGetValues("Set-Cookie", out var cook))
                            {
                                string lf_session = null;
                                foreach (string line in cook)
                                {
                                    if (string.IsNullOrWhiteSpace(line))
                                        continue;

                                    if (line.Contains("lf_session="))
                                    {
                                        lf_session = new Regex("lf_session=([^;]+)(;|$)").Match(line).Groups[1].Value;
                                        if (!string.IsNullOrWhiteSpace(lf_session))
                                            return $"lf_session={lf_session};";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return string.Empty;
        }
        #endregion

        #region LostfilmController
        static System.Net.Http.HttpClient cloudHttp = null;

        async static Task<bool> createHttp() 
        {
            string cookie = AppInit.conf.Lostfilm.cookie;
            if (string.IsNullOrWhiteSpace(cookie))
            {
                return false;

                cookie = await TakeLogin();
                if (string.IsNullOrWhiteSpace(cookie))
                    return false;
            }

            //var handler = new ClearanceHandler("http://ip:8191/")
            //{
            //    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.138 Safari/537.36",
            //    MaxTimeout = 60000
            //};

            cloudHttp = new System.Net.Http.HttpClient(); // handler
            cloudHttp.Timeout = TimeSpan.FromSeconds(AppInit.conf.jac.timeoutSeconds);
            cloudHttp.MaxResponseContentBufferSize = 10_000_000;
            cloudHttp.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.131 Safari/537.36");
            cloudHttp.DefaultRequestHeaders.Add("cookie", cookie);
            cloudHttp.DefaultRequestHeaders.Add("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            cloudHttp.DefaultRequestHeaders.Add("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5");
            cloudHttp.DefaultRequestHeaders.Add("cache-control", "no-cache");
            cloudHttp.DefaultRequestHeaders.Add("dnt", "1");
            cloudHttp.DefaultRequestHeaders.Add("pragma", "no-cache");
            cloudHttp.DefaultRequestHeaders.Add("sec-ch-ua", "\"Chromium\";v=\"92\", \" Not A;Brand\";v=\"99\", \"Google Chrome\";v=\"92\"");
            cloudHttp.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
            cloudHttp.DefaultRequestHeaders.Add("sec-fetch-dest", "document");
            cloudHttp.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
            cloudHttp.DefaultRequestHeaders.Add("sec-fetch-site", "none");
            cloudHttp.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
            cloudHttp.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");

            return true;
        }
        #endregion

        #region getTorrent
        async Task<byte[]> getTorrent(string episodeid)
        {
            try
            {
                // Получаем ссылку на поиск
                string v_search = await cloudHttp.GetStringAsync($"{AppInit.conf.Lostfilm.host}/v_search.php?a={episodeid}");
                string retreSearchUrl = new Regex("url=(\")?(https?://[^/]+/[^\"]+)").Match(v_search ?? "").Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(retreSearchUrl))
                {
                    // Загружаем HTML поиска
                    string shtml = await cloudHttp.GetStringAsync(retreSearchUrl);
                    if (!string.IsNullOrWhiteSpace(shtml))
                    {
                        var match = new Regex("<div class=\"inner-box--link main\"><a href=\"([^\"]+)\">([^<]+)</a></div>").Match(Regex.Replace(shtml, "[\n\r\t]+", ""));
                        while (match.Success)
                        {
                            if (Regex.IsMatch(match.Groups[2].Value, "(2160p|2060p|1440p|1080p|720p)", RegexOptions.IgnoreCase))
                            {
                                string torrentFile = match.Groups[1].Value;
                                string quality = Regex.Match(match.Groups[2].Value, "(2160p|2060p|1440p|1080p|720p)").Groups[1].Value;

                                if (!string.IsNullOrWhiteSpace(torrentFile) && !string.IsNullOrWhiteSpace(quality))
                                {
                                    byte[] torrent = await HttpClient.Download(torrentFile, referer: $"{AppInit.conf.Lostfilm.host}/");
                                    if (BencodeTo.Magnet(torrent) != null)
                                        return torrent;
                                }
                            }

                            match = match.NextMatch();
                        }
                    }
                }
            }
            catch { }

            return null;
        }
        #endregion

        #region parseMagnet
        static string TorrentFileMemKey(string episodeid) => $"lostfilm:parseMagnet:{episodeid}";

        async public Task<ActionResult> parseMagnet(string episodeid, bool usecache)
        {
            if (!AppInit.conf.Lostfilm.enable)
                return Content("disable");

            string key = TorrentFileMemKey(episodeid);
            if (Startup.memoryCache.TryGetValue(key, out byte[] _t))
                return File(_t, "application/x-bittorrent");

            if (usecache || Startup.memoryCache.TryGetValue($"{key}:error", out _))
            {
                if (await TorrentCache.Read(key) is var tc && tc.cache)
                    return File(tc.torrent, "application/x-bittorrent");

                return Content("error");
            }

            if (cloudHttp == null && await createHttp() == false)
                return Content("TakeLogin");

            _t = await getTorrent(episodeid);
            if (_t != null)
            {
                await TorrentCache.Write(key, _t);
                Startup.memoryCache.Set(key, _t, DateTime.Now.AddMinutes(Math.Max(1, AppInit.conf.jac.torrentCacheToMinutes)));
                return File(_t, "application/x-bittorrent");
            }
            else if (AppInit.conf.jac.emptycache)
                Startup.memoryCache.Set($"{key}:error", 0, DateTime.Now.AddMinutes(Math.Max(1, AppInit.conf.jac.torrentCacheToMinutes)));

            if (await TorrentCache.Read(key) is var tcache && tcache.cache)
                return File(tcache.torrent, "application/x-bittorrent");

            return Content("error");
        }
        #endregion


        #region parsePage
        async public static Task<bool> parsePage(string host, ConcurrentBag<TorrentDetails> torrents, string query)
        {
            if (!AppInit.conf.Lostfilm.enable)
                return false;

            if (cloudHttp == null && await createHttp() == false)
                return false;

            #region Кеш html
            string cachekey = $"lostfilm:{query}";
            var cread = await HtmlCache.Read(cachekey);
            bool validrq = cread.cache;

            if (cread.emptycache)
                return false;

            if (!cread.cache)
            {
                string html = await HttpClient.Get($"{AppInit.conf.Lostfilm.host}/search/?q={HttpUtility.UrlEncode(query)}", timeoutSeconds: AppInit.conf.jac.timeoutSeconds);

                if (html != null && html.Contains("onClick=\"FollowSerial("))
                {
                    string serie = Regex.Match(html, "href=\"/series/([^\"]+)\" class=\"no-decoration\"").Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(serie))
                    {
                        html = await HttpClient.Get($"{AppInit.conf.Lostfilm.host}/series/{serie}/seasons/", timeoutSeconds: AppInit.conf.jac.timeoutSeconds);
                        if (html != null && html.Contains("LostFilm.TV"))
                        {
                            cread.html = html;
                            await HtmlCache.Write(cachekey, cread.html);
                            validrq = true;
                        }
                    }
                }

                if (cread.html == null)
                {
                    HtmlCache.EmptyCache(cachekey);
                    return false;
                }
            }
            #endregion

            foreach (string row in cread.html.Split("<tr>").Skip(1))
            {
                try
                {
                    #region Локальный метод - Match
                    string Match(string val, string pattern, int index = 1)
                    {
                        string res = HttpUtility.HtmlDecode(new Regex(pattern, RegexOptions.IgnoreCase).Match(val).Groups[index].Value.Trim());
                        res = Regex.Replace(res, "[\n\r\t ]+", " ");
                        return res.Trim();
                    }
                    #endregion

                    if (string.IsNullOrWhiteSpace(row))
                        continue;

                    #region Дата создания
                    DateTime createTime = tParse.ParseCreateTime(Match(row, "data-released=\"([0-9]{2}\\.[0-9]{2}\\.[0-9]{4})\">([^<]+)</span>"), "dd.MM.yyyy");

                    if (createTime == default)
                        continue;
                    #endregion

                    #region Данные раздачи
                    string url = Match(cread.html, "href=\"/(series/[^/]+/seasons)\" class=\"item  active\">Гид по сериям</a>");
                    string sinfo = Match(row,"title=\"Перейти к серии\">([^<]+)</td>");
                    string name = Match(cread.html, "<h1 class=\"title-ru\" itemprop=\"name\">([^<]+)</h1>");
                    string originalname = Match(cread.html, "<h2 class=\"title-en\" itemprop=\"alternativeHeadline\">([^<]+)</h2>");

                    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(originalname) || string.IsNullOrWhiteSpace(sinfo))
                        continue;

                    url = $"{AppInit.conf.Lostfilm.host}/{url}";
                    #endregion

                    string episodeid = Match(row, "onclick=\"PlayEpisode\\('([0-9]+)'\\)\"");
                    if (string.IsNullOrWhiteSpace(episodeid))
                        continue;

                    if (!validrq && !TorrentCache.Exists(TorrentFileMemKey(episodeid)))
                        continue;

                    torrents.Add(new TorrentDetails()
                    {
                        trackerName = "lostfilm",
                        types = new string[] { "serial" },
                        url = url,
                        title = $"{name} / {originalname} / {sinfo} [{createTime.Year}, 1080p]",
                        sid = 1,
                        createTime = createTime,
                        parselink = $"{host}/lostfilm/parsemagnet?episodeid={episodeid}" + (!validrq ? "&usecache=true" : ""),
                        name = name,
                        originalname = originalname,
                        relased = createTime.Year
                    });
                }
                catch { }
            }

            return true;
        }
        #endregion
    }
}

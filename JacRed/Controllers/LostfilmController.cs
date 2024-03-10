using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Engine.Parse;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using Shared;
using JacRed.Engine;
using JacRed.Models;

namespace Lampac.Controllers.JAC
{
    [Route("lostfilm/[action]")]
    public class LostfilmController : JacBaseController
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
                    client.Timeout = TimeSpan.FromSeconds(jackett.timeoutSeconds);
                    client.MaxResponseContentBufferSize = 2000000; // 2MB
                    client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/75.0.3770.100 Safari/537.36");

                    var postParams = new Dictionary<string, string>
                    {
                        { "act", "users" },
                        { "type", "login" },
                        { "mail", jackett.Lostfilm.login.u },
                        { "pass", jackett.Lostfilm.login.p },
                        { "need_captcha", "" },
                        { "captcha", "" },
                        { "rem", "1" }
                    };

                    using (var postContent = new System.Net.Http.FormUrlEncodedContent(postParams))
                    {
                        using (var response = await client.PostAsync($"{jackett.Lostfilm.host}/ajaxik.users.php", postContent))
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
            string cookie = jackett.Lostfilm.cookie;
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
            cloudHttp.Timeout = TimeSpan.FromSeconds(jackett.timeoutSeconds);
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
                string v_search = await cloudHttp.GetStringAsync($"{jackett.Lostfilm.host}/v_search.php?a={episodeid}");
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
                                    byte[] torrent = await HttpClient.Download(torrentFile, referer: $"{jackett.Lostfilm.host}/");
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
        async public Task<ActionResult> parseMagnet(string episodeid)
        {
            if (!jackett.Lostfilm.enable)
                return Content("disable");

            string key = $"lostfilm:parseMagnet:{episodeid}";
            if (Startup.memoryCache.TryGetValue(key, out byte[] _t))
                return File(_t, "application/x-bittorrent");

            if (Startup.memoryCache.TryGetValue($"{key}:error", out _))
            {
                if (TorrentCache.Read(key) is var tc && tc.cache)
                    return File(tc.torrent, "application/x-bittorrent");

                return Content("error");
            }

            if (cloudHttp == null && await createHttp() == false)
                return Content("TakeLogin");

            _t = await getTorrent(episodeid);
            if (_t != null)
            {
                if (jackett.cache)
                {
                    TorrentCache.Write(key, _t);
                    Startup.memoryCache.Set(key, _t, DateTime.Now.AddMinutes(Math.Max(1, jackett.torrentCacheToMinutes)));
                }

                return File(_t, "application/x-bittorrent");
            }
            else if (jackett.emptycache && jackett.cache)
                Startup.memoryCache.Set($"{key}:error", 0, DateTime.Now.AddMinutes(1));

            if (TorrentCache.Read(key) is var tcache && tcache.cache)
                return File(tcache.torrent, "application/x-bittorrent");

            return Content("error");
        }
        #endregion


        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query)
        {
            if (!jackett.Lostfilm.enable)
                return Task.FromResult(false);

            return JackettCache.Invoke($"lostfilm:{query}", torrents, () => parsePage(host, query));
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query)
        {
            var torrents = new List<TorrentDetails>();

            if (cloudHttp == null && await createHttp() == false)
                return null;

            #region html
            bool validrq = false;
            string html = await HttpClient.Get($"{jackett.Lostfilm.host}/search/?q={HttpUtility.UrlEncode(query)}", timeoutSeconds: jackett.timeoutSeconds);

            if (html != null && html.Contains("onClick=\"FollowSerial("))
            {
                string serie = Regex.Match(html, "href=\"/series/([^\"]+)\" class=\"no-decoration\"").Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(serie))
                {
                    html = await HttpClient.Get($"{jackett.Lostfilm.host}/series/{serie}/seasons/", timeoutSeconds: jackett.timeoutSeconds);
                    if (html != null && html.Contains("LostFilm.TV"))
                        validrq = true;
                }
            }

            if (!validrq)
                return null;
            #endregion

            foreach (string row in html.Split("<tr>").Skip(1))
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
                    string url = Match(html, "href=\"/(series/[^/]+/seasons)\" class=\"item  active\">Гид по сериям</a>");
                    string sinfo = Match(row,"title=\"Перейти к серии\">([^<]+)</td>");
                    string name = Match(html, "<h1 class=\"title-ru\" itemprop=\"name\">([^<]+)</h1>");
                    string originalname = Match(html, "<h2 class=\"title-en\" itemprop=\"alternativeHeadline\">([^<]+)</h2>");

                    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(originalname) || string.IsNullOrWhiteSpace(sinfo))
                        continue;

                    url = $"{jackett.Lostfilm.host}/{url}";
                    #endregion

                    string episodeid = Match(row, "onclick=\"PlayEpisode\\('([0-9]+)'\\)\"");
                    if (string.IsNullOrWhiteSpace(episodeid))
                        continue;

                    torrents.Add(new TorrentDetails()
                    {
                        trackerName = "lostfilm",
                        types = new string[] { "serial" },
                        url = url,
                        title = $"{name} / {originalname} / {sinfo} [{createTime.Year}, 1080p]",
                        sid = 1,
                        createTime = createTime,
                        parselink = $"{host}/lostfilm/parsemagnet?episodeid={episodeid}",
                        name = name,
                        originalname = originalname,
                        relased = createTime.Year
                    });
                }
                catch { }
            }

            return torrents;
        }
        #endregion
    }
}

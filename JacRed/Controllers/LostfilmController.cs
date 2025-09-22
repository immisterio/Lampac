using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace JacRed.Controllers
{
    [Route("lostfilm/[action]")]
    public class LostfilmController : JacBaseController
    {
        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query)
        {
            if (!jackett.Lostfilm.enable || string.IsNullOrEmpty(jackett.Lostfilm.cookie ?? jackett.Lostfilm.login.u) || jackett.Lostfilm.showdown)
                return Task.FromResult(false);

            return Joinparse(torrents, () => parsePage(host, query));
        }
        #endregion


        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string episodeid)
        {
            if (!jackett.Lostfilm.enable)
                return Content("disable");

            var _t = await getTorrent(episodeid);
            if (_t != null)
                return File(_t, "application/x-bittorrent");

            return Content("error");
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query)
        {
            var proxyManager = new ProxyManager("lostfilm", jackett.Lostfilm);

            #region html
            bool validrq = false;
            string html = await Http.Get($"{jackett.Lostfilm.host}/search/?q={HttpUtility.UrlEncode(query)}", timeoutSeconds: jackett.timeoutSeconds, proxy: proxyManager.Get());

            if (html != null && html.Contains("onClick=\"FollowSerial("))
            {
                string serie = Regex.Match(html, "href=\"/series/([^\"]+)\" class=\"no-decoration\"").Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(serie))
                {
                    html = await Http.Get($"{jackett.Lostfilm.host}/series/{serie}/seasons/", timeoutSeconds: jackett.timeoutSeconds);
                    if (html != null && html.Contains("LostFilm.TV"))
                        validrq = true;
                }
            }

            if (!validrq)
            {
                consoleErrorLog("lostfilm");
                return null;
            }
            #endregion

            var torrents = new List<TorrentDetails>();

            foreach (string row in html.Split("<tr>").Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row))
                    continue;

                #region Локальный метод - Match
                string Match(string val, string pattern, int index = 1)
                {
                    string res = HttpUtility.HtmlDecode(new Regex(pattern, RegexOptions.IgnoreCase).Match(val).Groups[index].Value.Trim());
                    res = Regex.Replace(res, "[\n\r\t ]+", " ");
                    return res.Trim();
                }
                #endregion

                #region Данные раздачи
                DateTime createTime = tParse.ParseCreateTime(Match(row, "data-released=\"([0-9]{2}\\.[0-9]{2}\\.[0-9]{4})\">([^<]+)</span>"), "dd.MM.yyyy");

                string url = Match(html, "href=\"/(series/[^/]+/seasons)\" class=\"item  active\">Гид по сериям</a>");
                string sinfo = Match(row, "title=\"Перейти к серии\">([^<]+)</td>");
                string name = Match(html, "<h1 class=\"title-ru\" itemprop=\"name\">([^<]+)</h1>");
                string originalname = Match(html, "<h2 class=\"title-en\" itemprop=\"alternativeHeadline\">([^<]+)</h2>");

                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(originalname) || string.IsNullOrWhiteSpace(sinfo))
                    continue;
                #endregion

                string episodeid = Match(row, "onclick=\"PlayEpisode\\('([0-9]+)'\\)\"");
                if (string.IsNullOrWhiteSpace(episodeid))
                    continue;

                torrents.Add(new TorrentDetails()
                {
                    types = new string[] { "serial" },
                    url = $"{jackett.Lostfilm.host}/{url}",
                    title = $"{name} / {originalname} / {sinfo} [{createTime.Year}, 1080p]",
                    sid = 1,
                    createTime = createTime,
                    parselink = $"{host}/lostfilm/parsemagnet?episodeid={episodeid}",
                    name = name,
                    originalname = originalname,
                    relased = createTime.Year
                });
            }

            return torrents;
        }
        #endregion


        #region getTorrent
        async Task<byte[]> getTorrent(string episodeid)
        {
            try
            {
                string cookie = await getCookie();
                if (string.IsNullOrEmpty(cookie))
                    return null;

                var proxyManager = new ProxyManager("lostfilm", jackett.Lostfilm);
                var proxy = proxyManager.Get();

                // Получаем ссылку на поиск
                string v_search = await Http.Get($"{jackett.Lostfilm.host}/v_search.php?a={episodeid}", proxy: proxy, cookie: cookie);
                string retreSearchUrl = new Regex("url=(\")?(https?://[^/]+/[^\"]+)").Match(v_search ?? "").Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(retreSearchUrl))
                {
                    // Загружаем HTML поиска
                    string shtml = await Http.Get(retreSearchUrl, proxy: proxy, cookie: cookie);
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
                                    byte[] torrent = await Http.Download(torrentFile, referer: $"{jackett.Lostfilm.host}/", proxy: proxy, cookie: cookie);
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

        #region getCookie
        async static ValueTask<string> getCookie()
        {
            if (!string.IsNullOrEmpty(jackett.Lostfilm.cookie))
                return jackett.Lostfilm.cookie;

            string authKey = "Lostfilm:TakeLogin()";
            if (Startup.memoryCache.TryGetValue(authKey, out string _cookie))
                return _cookie;

            if (Startup.memoryCache.TryGetValue($"{authKey}:error", out _))
                return null;

            Startup.memoryCache.Set($"{authKey}:error", 0, TimeSpan.FromSeconds(20));

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
                                    string cookie = string.Empty;
                                    foreach (string line in cook)
                                    {
                                        if (string.IsNullOrWhiteSpace(line))
                                            continue;

                                        cookie += " " + line;
                                    }

                                    if (cookie.Contains("lf_session=") && cookie.Contains("lnk_uid="))
                                    {
                                        cookie = Regex.Replace(cookie.Trim(), ";$", "");
                                        Startup.memoryCache.Set(authKey, cookie, DateTime.Today.AddDays(1));
                                        return cookie;
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

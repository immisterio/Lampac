using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace JacRed.Controllers
{
    [Route("rutracker/[action]")]
    public class RutrackerController : JacBaseController
    {
        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query, string[] cats)
        {
            if (!jackett.Rutracker.enable || string.IsNullOrEmpty(jackett.Rutracker.cookie ?? jackett.Rutracker.login.u) || jackett.Rutracker.showdown)
                return Task.FromResult(false);

            return Joinparse(torrents, () => parsePage(host, query, cats));
        }
        #endregion


        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string id)
        {
            if (!jackett.Rutracker.enable)
                return Content("disable");

            string cookie = await getCookie();
            if (string.IsNullOrEmpty(cookie))
                return Content("cookie == null");

            var proxyManager = new ProxyManager("rutracker", jackett.Rutracker);

            #region Download
            if (jackett.Rutracker.priority == "torrent")
            {
                var _t = await Http.Download($"{jackett.Rutracker.host}/forum/dl.php?t={id}", proxy: proxyManager.Get(), cookie: cookie, referer: jackett.Rutracker.host);
                if (_t != null && BencodeTo.Magnet(_t) != null)
                    return File(_t, "application/x-bittorrent");
            }
            #endregion

            #region Magnet
            var fullNews = await Http.Get($"{jackett.Rutracker.host}/forum/viewtopic.php?t=" + id, proxy: proxyManager.Get(), cookie: cookie);
            if (fullNews != null)
            {
                string magnet = Regex.Match(fullNews, "href=\"(magnet:[^\"]+)\" class=\"(med )?med magnet-link\"").Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(magnet))
                    return Redirect(magnet);
            }
            #endregion

            return Content("error");
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query, string[] cats)
        {
            var torrents = new List<TorrentDetails>();
            var proxyManager = new ProxyManager("rutracker", jackett.Rutracker);

            #region Авторизация
            string cookie = await getCookie();
            if (string.IsNullOrEmpty(cookie))
            {
                consoleErrorLog("rutracker");
                return null;
            }
            #endregion

            #region Кеш html
            string html = await Http.Get($"{jackett.Rutracker.host}/forum/tracker.php?nm=" + HttpUtility.UrlEncode(query), proxy: proxyManager.Get(), cookie: cookie, timeoutSeconds: jackett.timeoutSeconds);

            if (html != null)
            {
                if (!html.Contains("id=\"logged-in-username\""))
                {
                    consoleErrorLog("rutracker");
                    return null;
                }
            }
            #endregion

            foreach (string row in html.Split("class=\"tCenter hl-tr\"").Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row))
                    continue;

                #region Локальный метод - Match
                string Match(string pattern, int index = 1)
                {
                    string res = HttpUtility.HtmlDecode(new Regex(pattern, RegexOptions.IgnoreCase).Match(row).Groups[index].Value.Trim());
                    res = Regex.Replace(res, "[\n\r\t ]+", " ");
                    return res.Trim();
                }
                #endregion

                #region Данные раздачи
                string title = Match("href=\"viewtopic.php\\?t=[0-9]+\">([^\n\r]+)</a>");
                title = Regex.Replace(title, "<[^>]+>", "");

                DateTime createTime = tParse.ParseCreateTime(Match("<p>([0-9]{2}-[^-<]+-[0-9]{2})</p>").Replace("-", " "), "dd.MM.yy");
                string viewtopic = Match("href=\"viewtopic.php\\?t=([0-9]+)\"");
                string tracker = Match("href=\"tracker.php\\?f=([0-9]+)");
                string _sid = Match("class=\"seedmed\">([0-9]+)");
                string _pir = Match("title=\"Личи\">([0-9]+)");
                string sizeName = Match("href=\"dl.php\\?t=[0-9]+\">([^<]+) &#8595;</a>").Replace("&nbsp;", " ");

                if (string.IsNullOrWhiteSpace(viewtopic) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(tracker))
                    continue;
                #endregion

                #region types
                string[] types = null;
                switch (tracker)
                {
                    case "22":
                    case "1666":
                    case "941":
                    case "1950":
                    case "2090":
                    case "2221":
                    case "2091":
                    case "2092":
                    case "2093":
                    case "2200":
                    case "2540":
                    case "934":
                    case "505":
                    case "124":
                    case "1457":
                    case "2199":
                    case "313":
                    case "312":
                    case "1247":
                    case "2201":
                    case "2339":
                    case "140":
                    case "252":
                    case "2198":
                        types = new string[] { "movie" };
                        break;
                    case "2343":
                    case "930":
                    case "2365":
                    case "208":
                    case "539":
                    case "209":
                        types = new string[] { "multfilm" };
                        break;
                    case "921":
                    case "815":
                    case "1460":
                        types = new string[] { "multserial" };
                        break;
                    case "842":
                    case "235":
                    case "242":
                    case "819":
                    case "1531":
                    case "721":
                    case "1102":
                    case "1120":
                    case "1214":
                    case "489":
                    case "387":
                    case "9":
                    case "81":
                    case "119":
                    case "1803":
                    case "266":
                    case "193":
                    case "1690":
                    case "1459":
                    case "825":
                    case "1248":
                    case "1288":
                    case "325":
                    case "534":
                    case "694":
                    case "704":
                    case "915":
                    case "1939":
                        types = new string[] { "serial" };
                        break;
                    case "1105":
                    case "2491":
                    case "1389":
                        types = new string[] { "anime" };
                        break;
                    case "709":
                        types = new string[] { "documovie" };
                        break;
                    case "46":
                    case "671":
                    case "2177":
                    case "2538":
                    case "251":
                    case "98":
                    case "97":
                    case "851":
                    case "2178":
                    case "821":
                    case "2076":
                    case "56":
                    case "2123":
                    case "876":
                    case "2139":
                    case "1467":
                    case "1469":
                    case "249":
                    case "552":
                    case "500":
                    case "2112":
                    case "1327":
                    case "1468":
                    case "2168":
                    case "2160":
                    case "314":
                    case "1281":
                    case "2110":
                    case "979":
                    case "2169":
                    case "2164":
                    case "2166":
                    case "2163":
                        types = new string[] { "docuserial", "documovie" };
                        break;
                    case "24":
                    case "1959":
                    case "939":
                    case "1481":
                    case "113":
                    case "115":
                    case "882":
                    case "1482":
                    case "393":
                    case "2537":
                    case "532":
                    case "827":
                        types = new string[] { "tvshow" };
                        break;
                    case "2103":
                    case "2522":
                    case "2485":
                    case "2486":
                    case "2479":
                    case "2089":
                    case "1794":
                    case "845":
                    case "2312":
                    case "343":
                    case "2111":
                    case "1527":
                    case "2069":
                    case "1323":
                    case "2009":
                    case "2000":
                    case "2010":
                    case "2006":
                    case "2007":
                    case "2005":
                    case "259":
                    case "2004":
                    case "1999":
                    case "2001":
                    case "2002":
                    case "283":
                    case "1997":
                    case "2003":
                    case "1608":
                    case "1609":
                    case "2294":
                    case "1229":
                    case "1693":
                    case "2532":
                    case "136":
                    case "592":
                    case "2533":
                    case "1952":
                    case "1621":
                    case "2075":
                    case "1668":
                    case "1613":
                    case "1614":
                    case "1623":
                    case "1615":
                    case "1630":
                    case "2425":
                    case "2514":
                    case "1616":
                    case "2014":
                    case "1442":
                    case "1491":
                    case "1987":
                    case "1617":
                    case "1620":
                    case "1998":
                    case "1343":
                    case "751":
                    case "1697":
                    case "255":
                    case "260":
                    case "261":
                    case "256":
                    case "1986":
                    case "660":
                    case "1551":
                    case "626":
                    case "262":
                    case "1326":
                    case "978":
                    case "1287":
                    case "1188":
                    case "1667":
                    case "1675":
                    case "257":
                    case "875":
                    case "263":
                    case "2073":
                    case "550":
                    case "2124":
                    case "1470":
                    case "528":
                    case "486":
                    case "854":
                    case "2079":
                    case "1336":
                    case "2171":
                    case "1339":
                    case "2455":
                    case "1434":
                    case "2350":
                    case "1472":
                    case "2068":
                    case "2016":
                        types = new string[] { "sport" };
                        break;
                }

                if (cats != null)
                {
                    if (types == null)
                        continue;

                    bool isok = false;
                    foreach (string cat in cats)
                    {
                        if (types.Contains(cat))
                            isok = true;
                    }

                    if (!isok)
                        continue;
                }
                #endregion

                int.TryParse(_sid, out int sid);
                int.TryParse(_pir, out int pir);

                torrents.Add(new TorrentDetails()
                {
                    types = types,
                    url = $"{jackett.Rutracker.host}/forum/viewtopic.php?t={viewtopic}",
                    title = title,
                    sid = sid,
                    pir = pir,
                    sizeName = sizeName,
                    createTime = createTime,
                    parselink = $"{host}/rutracker/parsemagnet?id={viewtopic}"
                });
            }

            return torrents;
        }
        #endregion


        #region getCookie
        async static ValueTask<string> getCookie()
        {
            if (!string.IsNullOrEmpty(jackett.Rutracker.cookie))
                return jackett.Rutracker.cookie;

            string authKey = "Rutracker:TakeLogin()";
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
                        { "login_username", jackett.Rutracker.login.u },
                        { "login_password", jackett.Rutracker.login.p },
                        { "login", "Вход" }
                    };

                        using (var postContent = new System.Net.Http.FormUrlEncodedContent(postParams))
                        {
                            using (var response = await client.PostAsync($"{jackett.Rutracker.host}/forum/login.php", postContent))
                            {
                                if (response.Headers.TryGetValues("Set-Cookie", out var cook))
                                {
                                    string session = null;
                                    foreach (string line in cook)
                                    {
                                        if (string.IsNullOrWhiteSpace(line))
                                            continue;

                                        if (line.Contains("bb_session="))
                                            session = new Regex("bb_session=([^;]+)(;|$)").Match(line).Groups[1].Value;
                                    }

                                    if (!string.IsNullOrWhiteSpace(session))
                                    {
                                        string cookie = $"bb_ssl=1; bb_session={session};";
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

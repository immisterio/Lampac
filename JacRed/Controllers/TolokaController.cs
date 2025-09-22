using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace JacRed.Controllers
{
    [Route("toloka/[action]")]
    public class TolokaController : JacBaseController
    {
        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query, string[] cats)
        {
            if (!jackett.Toloka.enable || string.IsNullOrEmpty(jackett.Toloka.cookie ?? jackett.Toloka.login.u) || jackett.Toloka.showdown)
                return Task.FromResult(false);

            return Joinparse(torrents, () => parsePage(host, query, cats));
        }
        #endregion


        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string id)
        {
            if (!jackett.Toloka.enable)
                return Content("disable");

            string cookie = await getCookie();
            if (string.IsNullOrEmpty(cookie))
                return Content("cookie == null");

            var proxyManager = new ProxyManager("toloka", jackett.Toloka);

            byte[] _t = await Http.Download($"{jackett.Toloka.host}/download.php?id={id}", proxy: proxyManager.Get(), cookie: cookie, referer: jackett.Toloka.host);
            if (_t != null && BencodeTo.Magnet(_t) != null)
                return File(_t, "application/x-bittorrent");

            return Content("error");
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query, string[] cats)
        {
            #region Авторизация
            string cookie = await getCookie();
            if (string.IsNullOrEmpty(cookie))
            {
                consoleErrorLog("toloka");
                return null;
            }
            #endregion

            #region html
            var proxyManager = new ProxyManager("toloka", jackett.Toloka);

            string html = await Http.Get($"{jackett.Toloka.host}/tracker.php?prev_sd=0&prev_a=0&prev_my=0&prev_n=0&prev_shc=0&prev_shf=1&prev_sha=1&prev_cg=0&prev_ct=0&prev_at=0&prev_nt=0&prev_de=0&prev_nd=0&prev_tcs=1&prev_shs=0&f%5B%5D=-1&o=1&s=2&tm=-1&shf=1&sha=1&tcs=1&sns=-1&sds=-1&nm={HttpUtility.UrlEncode(query)}&pn=&send=%D0%9F%D0%BE%D1%88%D1%83%D0%BA", proxy: proxyManager.Get(), cookie: cookie, timeoutSeconds: jackett.timeoutSeconds);

            if (html != null && html.Contains("<html lang=\"uk\""))
            {
                if (!html.Contains(">Вихід"))
                {
                    consoleErrorLog("toloka");
                    return null;
                }
            }
            #endregion

            var torrents = new List<TorrentDetails>();

            foreach (string row in html.Split("</tr>"))
            {
                if (string.IsNullOrWhiteSpace(row) || Regex.IsMatch(row, "Збір коштів", RegexOptions.IgnoreCase))
                    continue;

                #region Локальный метод - Match
                string Match(string pattern, int index = 1)
                {
                    string res = HttpUtility.HtmlDecode(new Regex(pattern, RegexOptions.IgnoreCase).Match(row).Groups[index].Value.Trim());
                    res = Regex.Replace(res, "[\n\r\t ]+", " ");
                    return res.Trim();
                }
                #endregion

                #region Дата создания
                string _createTime = Match("class=\"gensmall\">([0-9]{4}-[0-9]{2}-[0-9]{2})").Replace("-", ".");
                DateTime.TryParse(_createTime, out DateTime createTime);
                #endregion

                #region Данные раздачи
                string url = Match("class=\"topictitle genmed\"><a class=\"[^\"]+\" href=\"(t[0-9]+)\"");
                string title = Match("class=\"topictitle genmed\"><a [^>]+><b>([^<]+)</b></a>");
                string downloadid = Match("href=\"download.php\\?id=([0-9]+)\"");
                string tracker = Match("class=\"gen\" href=\"tracker.php\\?f=([0-9]+)");
                string _sid = Match("class=\"seedmed\"><b>([0-9]+)");
                string _pir = Match("class=\"leechmed\"><b>([0-9]+)");
                string sizeName = Match("class=\"gensmall\">([0-9\\.]+ (MB|GB))</td>");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(downloadid) || string.IsNullOrWhiteSpace(tracker) || sizeName == "0 B")
                    continue;
                #endregion

                #region Парсим раздачи
                int relased = 0;
                string name = null, originalname = null;

                if (tracker is "16" or "96" or "19" or "139" or "12" or "131" or "84" or "42")
                {
                    #region Фильмы
                    // Незворотність / Irréversible / Irreversible (2002) AVC Ukr/Fre | Sub Eng
                    var g = Regex.Match(title, "^([^/\\(\\[]+)/[^/\\(\\[]+/([^/\\(\\[]+) \\(([0-9]{4})(\\)|-)").Groups;
                    if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                    {
                        name = g[1].Value.Trim();
                        originalname = g[2].Value.Trim();

                        if (int.TryParse(g[3].Value, out int _yer))
                            relased = _yer;
                    }
                    else
                    {
                        // Мій рік у Нью-Йорку / My Salinger Year (2020) Ukr/Eng
                        g = Regex.Match(title, "^([^/\\(\\[]+)/([^/\\(\\[]+) \\(([0-9]{4})(\\)|-)").Groups;
                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                        {
                            name = g[1].Value.Trim();
                            originalname = g[2].Value.Trim();

                            if (int.TryParse(g[3].Value, out int _yer))
                                relased = _yer;
                        }
                        else
                        {
                            // Хроніка надій та ілюзій. Дзеркало історії. (83 серії) (2001-2003) PDTVRip
                            g = Regex.Match(title, "^([^/\\(\\[]+) \\([^\\)]+\\) \\(([0-9]{4})(\\)|-)").Groups;
                            if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                            {
                                name = g[1].Value;

                                if (int.TryParse(g[2].Value, out int _yer))
                                    relased = _yer;
                            }
                            else
                            {
                                // Берестечко. Битва за Україну (2015-2016) DVDRip-AVC
                                g = Regex.Match(title, "^([^/\\(\\[]+) \\(([0-9]{4})(\\)|-)").Groups;
                                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                                {
                                    name = g[1].Value;

                                    if (int.TryParse(g[2].Value, out int _yer))
                                        relased = _yer;
                                }
                            }
                        }
                    }
                    #endregion
                }
                else if (tracker is "32" or "173" or "174" or "44" or "230" or "226" or "227" or "228" or "229" or "127" or "124" or "125" or "132")
                {
                    #region Сериалы
                    // Атака титанів (Attack on Titan) (Сезон 1) / Shingeki no Kyojin (Season 1) (2013) BDRip 720р
                    var g = Regex.Match(title, "^([^/\\(\\[]+) \\([^\\)]+\\) \\([^\\)]+\\) ?/([^/\\(\\[]+) \\([^\\)]+\\) \\(([0-9]{4})(\\)|-)").Groups;
                    if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                    {
                        name = g[1].Value.Trim();
                        originalname = g[2].Value.Trim();

                        if (int.TryParse(g[3].Value, out int _yer))
                            relased = _yer;
                    }
                    else
                    {
                        // Дім з прислугою (Сезон 2, серії 1-8) / Servant (Season 2, episodes 1-8) (2021) WEB-DLRip-AVC Ukr/Eng
                        g = Regex.Match(title, "^([^/\\(\\[]+) \\([^\\)]+\\) ?/([^/\\(\\[]+) \\([^\\)]+\\) \\(([0-9]{4})(\\)|-)").Groups;
                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                        {
                            name = g[1].Value.Trim();
                            originalname = g[2].Value.Trim();

                            if (int.TryParse(g[3].Value, out int _yer))
                                relased = _yer;
                        }
                        else
                        {
                            // Детективне агентство прекрасних хлопчиків (08 з 12) / Bishounen Tanteidan (2021) BDRip 1080p Ukr/Jap | Ukr Sub
                            g = Regex.Match(title, "^([^/\\(\\[]+) (\\(|\\[)[^\\)\\]]+(\\)|\\]) ?/([^/\\(\\[]+) \\(([0-9]{4})(\\)|-)").Groups;
                            if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[4].Value) && !string.IsNullOrWhiteSpace(g[5].Value))
                            {
                                name = g[1].Value.Trim();
                                originalname = g[4].Value.Trim();

                                if (int.TryParse(g[5].Value, out int _yer))
                                    relased = _yer;
                            }
                            else
                            {
                                // Яйця Дракона / Dragon Ball (01-31 з 153) (1986-1989) BDRip 1080p H.265
                                // Томо — дівчина! / Tomo-chan wa Onnanoko! (Сезон 1, серії 01-02 з 13) (2023) WEBDL 1080p H.265 Ukr/Jap | sub Ukr
                                g = Regex.Match(title, "^([^/\\(\\[]+)/([^/\\(\\[]+) \\([^\\)]+\\) \\(([0-9]{4})(\\)|-)").Groups;
                                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                                {
                                    name = g[1].Value.Trim();
                                    originalname = g[2].Value.Trim();

                                    if (int.TryParse(g[3].Value, out int _yer))
                                        relased = _yer;
                                }
                                else
                                {
                                    // Людина-бензопила / チェンソーマン /Chainsaw Man (сезон 1, серії 8 з 12) (2022) WEBRip 1080p
                                    g = Regex.Match(title, "^([^/\\(\\[]+)/[^/\\(\\[]+/([^/\\(\\[]+) \\([^\\)]+\\) \\(([0-9]{4})(\\)|-)").Groups;
                                    if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                                    {
                                        name = g[1].Value.Trim();
                                        originalname = g[2].Value.Trim();

                                        if (int.TryParse(g[3].Value, out int _yer))
                                            relased = _yer;
                                    }
                                    else
                                    {
                                        // МастерШеф. 10 сезон (1-18 епізоди) (2020) IPTVRip 400p
                                        g = Regex.Match(title, "^([^/\\(\\[]+) \\([^\\)]+\\) \\(([0-9]{4})(\\)|-)").Groups;
                                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                                        {
                                            name = g[1].Value.Trim();

                                            if (int.TryParse(g[2].Value, out int _yer))
                                                relased = _yer;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                }
                #endregion

                #region types
                string[] types = null;
                switch (tracker)
                {
                    case "16":
                    case "96":
                    case "42":
                        types = new string[] { "movie" };
                        break;
                    case "19":
                    case "139":
                    case "84":
                        types = new string[] { "multfilm" };
                        break;
                    case "32":
                    case "173":
                    case "124":
                        types = new string[] { "serial" };
                        break;
                    case "174":
                    case "44":
                    case "125":
                        types = new string[] { "multserial" };
                        break;
                    case "226":
                    case "227":
                    case "228":
                    case "229":
                    case "230":
                    case "12":
                    case "131":
                        types = new string[] { "docuserial", "documovie" };
                        break;
                    case "127":
                        types = new string[] { "anime" };
                        break;
                    case "132":
                        types = new string[] { "tvshow" };
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
                    url = $"{jackett.Toloka.host}/{url}",
                    title = title,
                    sid = sid,
                    pir = pir,
                    sizeName = sizeName,
                    createTime = createTime,
                    parselink = $"{host}/toloka/parsemagnet?id={downloadid}",
                    name = name,
                    originalname = originalname,
                    relased = relased
                });
            }

            return torrents;
        }
        #endregion


        #region getCookie
        async static ValueTask<string> getCookie()
        {
            if (!string.IsNullOrEmpty(jackett.Toloka.cookie))
                return jackett.Toloka.cookie;

            string authKey = "Toloka:TakeLogin()";
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
                        { "username", jackett.Toloka.login.u },
                        { "password", jackett.Toloka.login.p },
                        { "autologin", "on" },
                        { "ssl", "on" },
                        { "redirect", "index.php?" },
                        { "login", "Вхід" }
                    };

                        using (var postContent = new System.Net.Http.FormUrlEncodedContent(postParams))
                        {
                            using (var response = await client.PostAsync($"{jackett.Toloka.host}/login.php", postContent))
                            {
                                if (response.Headers.TryGetValues("Set-Cookie", out var cook))
                                {
                                    string toloka_sid = null, toloka_data = null;
                                    foreach (string line in cook)
                                    {
                                        if (string.IsNullOrWhiteSpace(line))
                                            continue;

                                        if (line.Contains("toloka_sid="))
                                            toloka_sid = new Regex("toloka_sid=([^;]+)(;|$)").Match(line).Groups[1].Value;

                                        if (line.Contains("toloka_data="))
                                            toloka_data = new Regex("toloka_data=([^;]+)(;|$)").Match(line).Groups[1].Value;
                                    }

                                    if (!string.IsNullOrWhiteSpace(toloka_sid) && !string.IsNullOrWhiteSpace(toloka_data))
                                    {
                                        string cookie = $"toloka_sid={toloka_sid}; toloka_ssl=1; toloka_data={toloka_data};";
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

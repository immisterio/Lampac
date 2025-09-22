using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace JacRed.Controllers
{
    [Route("kinozal/[action]")]
    public class KinozalController : JacBaseController
    {
        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query, string[] cats)
        {
            if (!jackett.Kinozal.enable || jackett.Kinozal.showdown)
                return Task.FromResult(false);

            return Joinparse(torrents, () => parsePage(host, query, cats));
        }
        #endregion


        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string id)
        {
            if (!jackett.Kinozal.enable)
                return Content("disable");

            var proxyManager = new ProxyManager("kinozal", jackett.Kinozal);

            #region Download
            if (jackett.Kinozal.cookie != null || Cookie != null)
            {
                var _t = await Http.Download("http://dl.kinozal.tv/download.php?id=" + id, proxy: proxyManager.Get(), cookie: jackett.Kinozal.cookie ?? Cookie, referer: jackett.Kinozal.host, timeoutSeconds: 10);
                if (_t != null && BencodeTo.Magnet(_t) != null)
                    return File(_t, "application/x-bittorrent");
            }
            #endregion

            string srv_details = await Http.Post($"{jackett.Kinozal.host}/get_srv_details.php?id={id}&action=2", $"id={id}&action=2", "__cfduid=d476ac2d9b5e18f2b67707b47ebd9b8cd1560164391; uid=20520283; pass=ouV5FJdFCd;", proxy: proxyManager.Get(), timeoutSeconds: 10);
            if (srv_details != null)
            {
                string torrentHash = new Regex("<ul><li>Инфо хеш: +([^<]+)</li>").Match(srv_details).Groups[1].Value;
                if (!string.IsNullOrEmpty(torrentHash))
                    return Redirect($"magnet:?xt=urn:btih:{torrentHash}");
            }

            proxyManager.Refresh();
            return Content("error");
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query, string[] cats)
        {
            var torrents = new List<TorrentDetails>();
            var proxyManager = new ProxyManager("kinozal", jackett.Kinozal);

            string html = await Http.Get($"{jackett.Kinozal.host}/browse.php?s={HttpUtility.UrlEncode(query)}&g=0&c=0&v=0&d=0&w=0&t=0&f=0", proxy: proxyManager.Get(), timeoutSeconds: jackett.timeoutSeconds);

            if (html != null && html.Contains("Кинозал.ТВ</title>"))
            {
                if (!html.Contains(">Выход</a>") && !string.IsNullOrWhiteSpace(jackett.Kinozal.login.u) && !string.IsNullOrWhiteSpace(jackett.Kinozal.login.p))
                    TakeLogin();
            }
            else if (html == null)
            {
                consoleErrorLog("kinozal");
                proxyManager.Refresh();
                return null;
            }

            foreach (string row in Regex.Split(html, "<tr class=('first bg'|bg)>").Skip(1))
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

                #region Дата создания
                DateTime createTime = default;

                if (row.Contains("<td class='s'>сегодня"))
                {
                    createTime = DateTime.Today;
                }
                else if (row.Contains("<td class='s'>вчера"))
                {
                    createTime = DateTime.Today.AddDays(-1);
                }
                else
                {
                    createTime = tParse.ParseCreateTime(Match("<td class='s'>([0-9]{2}.[0-9]{2}.[0-9]{4}) в [0-9]{2}:[0-9]{2}"), "dd.MM.yyyy");
                }
                #endregion

                #region Данные раздачи
                string url = Match("href=\"/(details.php\\?id=[0-9]+)\"");
                string tracker = Match("src=\"/pic/cat/([0-9]+)\\.gif\"");
                string title = Match("class=\"r[0-9]+\">([^<]+)");
                string _sid = Match("<td class='sl_s'>([0-9]+)");
                string _pir = Match("<td class='sl_p'>([0-9]+)");
                string sizeName = Match("<td class='s'>([0-9\\.,]+ (МБ|ГБ))");

                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(tracker))
                    continue;
                #endregion

                // Id новости
                string id = Match("href=\"/details.php\\?id=([0-9]+)\"");
                if (string.IsNullOrEmpty(id))
                    continue;

                #region types
                string[] types = new string[] { };
                switch (tracker)
                {
                    case "1002":
                    case "8":
                    case "6":
                    case "15":
                    case "17":
                    case "35":
                    case "39":
                    case "13":
                    case "14":
                    case "24":
                    case "11":
                    case "10":
                    case "9":
                    case "47":
                    case "18":
                    case "37":
                    case "12":
                    case "7":
                    case "16":
                        types = new string[] { "movie" };
                        break;
                    case "45":
                    case "46":
                        types = new string[] { "serial" };
                        break;
                    case "21":
                    case "22":
                        types = new string[] { "multfilm", "multserial" };
                        break;
                    case "20":
                        types = new string[] { "anime" };
                        break;
                    case "1006":
                    case "48":
                    case "49":
                    case "50":
                    case "38":
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
                    url = $"{jackett.Kinozal.host}/{url}",
                    title = title,
                    sid = sid,
                    pir = pir,
                    sizeName = sizeName,
                    createTime = createTime,
                    parselink = $"{host}/kinozal/parsemagnet?id={id}"
                });
            }

            return torrents;
        }
        #endregion


        #region Cookie / TakeLogin
        static string Cookie;

        async static void TakeLogin()
        {
            string authKey = "kinozal:TakeLogin()";
            if (Startup.memoryCache.TryGetValue(authKey, out _))
                return;

            Startup.memoryCache.Set(authKey, 0, AppInit.conf.multiaccess ? TimeSpan.FromMinutes(2) : TimeSpan.FromSeconds(20));

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
                        client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.51 Safari/537.36");
                        client.DefaultRequestHeaders.Add("cache-control", "no-cache");
                        client.DefaultRequestHeaders.Add("dnt", "1");
                        client.DefaultRequestHeaders.Add("origin", jackett.Kinozal.host);
                        client.DefaultRequestHeaders.Add("pragma", "no-cache");
                        client.DefaultRequestHeaders.Add("referer", $"{jackett.Kinozal.host}/");
                        client.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");

                        var postParams = new Dictionary<string, string>
                    {
                        { "username", jackett.Kinozal.login.u },
                        { "password", jackett.Kinozal.login.p },
                        { "returnto", "" }
                    };

                        using (var postContent = new System.Net.Http.FormUrlEncodedContent(postParams))
                        {
                            using (var response = await client.PostAsync($"{jackett.Kinozal.host}/takelogin.php", postContent))
                            {
                                if (response.Headers.TryGetValues("Set-Cookie", out var cook))
                                {
                                    string uid = null, pass = null;
                                    foreach (string line in cook)
                                    {
                                        if (string.IsNullOrWhiteSpace(line))
                                            continue;

                                        if (line.Contains("uid="))
                                            uid = new Regex("uid=([0-9]+)").Match(line).Groups[1].Value;

                                        if (line.Contains("pass="))
                                            pass = new Regex("pass=([^;]+)(;|$)").Match(line).Groups[1].Value;
                                    }

                                    if (!string.IsNullOrWhiteSpace(uid) && !string.IsNullOrWhiteSpace(pass))
                                        Cookie = $"uid={uid}; pass={pass};";
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }
        #endregion
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace JacRed.Controllers
{
    [Route("nnmclub/[action]")]
    public class NNMClubController : JacBaseController
    {
        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query, string[] cats)
        {
            if (!jackett.NNMClub.enable || jackett.NNMClub.showdown)
                return Task.FromResult(false);

            return Joinparse(torrents, () => parsePage(host, query, cats));
        }
        #endregion


        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string id)
        {
            if (!jackett.NNMClub.enable)
                return Content("disable");

            var proxyManager = new ProxyManager("nnmclub", jackett.NNMClub);

            #region html
            string html = await Http.Get($"{jackett.NNMClub.host}/forum/viewtopic.php?t=" + id, proxy: proxyManager.Get());
            string magnet = new Regex("href=\"(magnet:[^\"]+)\" title=\"Примагнититься\"").Match(html ?? string.Empty).Groups[1].Value;

            if (html == null)
            {
                proxyManager.Refresh();
                return Content("error");
            }
            #endregion

            #region download torrent
            if (jackett.NNMClub.cookie != null || Cookie != null)
            {
                string downloadid = new Regex("href=\"download\\.php\\?id=([0-9]+)\"").Match(html).Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(downloadid))
                {
                    byte[] _t = await Http.Download($"{jackett.NNMClub.host}/forum/download.php?id={downloadid}", proxy: proxyManager.Get(), cookie: jackett.NNMClub.cookie ?? Cookie, referer: jackett.NNMClub.host);
                    if (_t != null && BencodeTo.Magnet(_t) != null)
                        return File(_t, "application/x-bittorrent");
                }
            }
            #endregion

            if (string.IsNullOrEmpty(magnet))
            {
                proxyManager.Refresh();
                return Content("error");
            }

            return Redirect(magnet);
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query, string[] cats)
        {
            var torrents = new List<TorrentDetails>();
            var proxyManager = new ProxyManager("nnmclub", jackett.NNMClub);

            #region html
            string data = $"prev_sd=0&prev_a=0&prev_my=0&prev_n=0&prev_shc=0&prev_shf=1&prev_sha=1&prev_shs=0&prev_shr=0&prev_sht=0&o=1&s=2&tm=-1&shf=1&sha=1&ta=-1&sns=-1&sds=-1&nm={HttpUtility.UrlEncode(query, Encoding.GetEncoding(1251))}&pn=&submit=%CF%EE%E8%F1%EA";
            string html = await Http.Post($"{jackett.NNMClub.host}/forum/tracker.php", new System.Net.Http.StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded"), encoding: Encoding.GetEncoding(1251), proxy: proxyManager.Get(), timeoutSeconds: jackett.timeoutSeconds);

            if (html != null && html.Contains("NNM-Club</title>"))
            {
                if (!html.Contains(">Выход") && !string.IsNullOrWhiteSpace(jackett.NNMClub.login.u) && !string.IsNullOrWhiteSpace(jackett.NNMClub.login.p))
                    TakeLogin();
            }
            else if (html == null)
            {
                consoleErrorLog("nnmclub");
                proxyManager.Refresh();
                return null;
            }
            #endregion

            foreach (string row in html.Split("</tr>"))
            {
                #region Локальный метод - Match
                string Match(string pattern, int index = 1)
                {
                    string res = HttpUtility.HtmlDecode(new Regex(pattern, RegexOptions.IgnoreCase).Match(row).Groups[index].Value.Trim());
                    res = Regex.Replace(res, "[\n\r\t ]+", " ");
                    return res.Trim();
                }
                #endregion

                #region Данные раздачи
                string url = Match("href=\"(viewtopic.php\\?t=[0-9]+)\"");
                string viewtopic = Match("href=\"viewtopic.php\\?t=([0-9]+)\"");
                string tracker = Match("class=\"gen\" href=\"tracker.php\\?f=([0-9]+)");

                string title = Match("class=\"genmed topictitle\" [^>]+><b>([^<]+)</b>");
                string _sid = Match("class=\"seedmed\"><b>([0-9]+)</b><");
                string _pir = Match("class=\"leechmed\"><b>([0-9]+)</b></td>");
                string sizeName = Match("class=\"gensmall\"><u>[^<]+</u> ([^<]+)</td>");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(viewtopic) || string.IsNullOrWhiteSpace(tracker))
                    continue;

                if (tracker == "913" && !title.Contains("UKR"))
                    continue;
                #endregion

                #region types
                string[] types = null;
                switch (tracker)
                {
                    case "270":
                    case "221":
                    case "882":
                    case "225":
                    case "227":
                    case "913":
                    case "218":
                    case "954":
                    case "1293":
                    case "1296":
                    case "1299":
                    case "682":
                    case "884":
                    case "693":
                        types = new string[] { "movie" };
                        break;
                    case "769":
                    case "768":
                        types = new string[] { "serial" };
                        break;
                    case "713":
                    case "576":
                    case "610":
                        types = new string[] { "docuserial", "documovie" };
                        break;
                    case "731":
                    case "733":
                    case "1329":
                    case "1330":
                    case "1331":
                    case "1332":
                    case "1336":
                    case "1337":
                    case "1338":
                    case "1339":
                        types = new string[] { "multfilm" };
                        break;
                    case "658":
                    case "232":
                        types = new string[] { "multserial" };
                        break;
                    case "623":
                    case "622":
                    case "621":
                    case "632":
                    case "627":
                    case "626":
                    case "625":
                    case "644":
                        types = new string[] { "anime" };
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
                    url = $"{jackett.NNMClub.host}/{url}",
                    title = title,
                    sid = sid,
                    pir = pir,
                    sizeName = sizeName,
                    parselink = $"{host}/nnmclub/parsemagnet?id={viewtopic}",
                    createTime = tParse.ParseCreateTime(Match("title=\"Добавлено\" class=\"gensmall\"><u>[0-9]+</u> ([0-9]{2}-[0-9]{2}-[0-9]{4}<br>[^<]+)</td>").Replace("<br>", " "), "dd-MM-yyyy HH:mm")
                });
            }

            return torrents;
        }
        #endregion


        #region Cookie / TakeLogin
        static string Cookie;

        async static void TakeLogin()
        {
            string authKey = "nnmclub:TakeLogin()";
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
                        client.DefaultRequestHeaders.Add("origin", jackett.NNMClub.host);
                        client.DefaultRequestHeaders.Add("pragma", "no-cache");
                        client.DefaultRequestHeaders.Add("referer", $"{jackett.NNMClub.host}/");
                        client.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");

                        var postParams = new Dictionary<string, string>
                    {
                        { "redirect", "%2F" },
                        { "username", jackett.NNMClub.login.u },
                        { "password", jackett.NNMClub.login.p },
                        { "autologin", "on" },
                        { "login", "%C2%F5%EE%E4" }
                    };

                        using (var postContent = new System.Net.Http.FormUrlEncodedContent(postParams))
                        {
                            using (var response = await client.PostAsync($"{jackett.NNMClub.host}/forum/login.php", postContent))
                            {
                                if (response.Headers.TryGetValues("Set-Cookie", out var cook))
                                {
                                    string data = null, sid = null;
                                    foreach (string line in cook)
                                    {
                                        if (string.IsNullOrWhiteSpace(line))
                                            continue;

                                        if (line.Contains("phpbb2mysql_4_data="))
                                            data = new Regex("phpbb2mysql_4_data=([^;]+)(;|$)").Match(line).Groups[1].Value;

                                        if (line.Contains("phpbb2mysql_4_sid="))
                                            sid = new Regex("phpbb2mysql_4_sid=([^;]+)(;|$)").Match(line).Groups[1].Value;
                                    }

                                    if (!string.IsNullOrWhiteSpace(data) && !string.IsNullOrWhiteSpace(sid))
                                        Cookie = $"phpbb2mysql_4_data={data}; phpbb2mysql_4_sid={sid};";
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

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
using Shared.Engine.CORE;
using JacRed.Engine;
using JacRed.Models;

namespace Lampac.Controllers.JAC
{
    [Route("kinozal/[action]")]
    public class KinozalController : JacBaseController
    {
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
                var clientHandler = new System.Net.Http.HttpClientHandler()
                {
                    AllowAutoRedirect = false
                };

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
            catch { }
        }
        #endregion

        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string id)
        {
            if (!jackett.Kinozal.enable)
                return Content("disable");

            #region Кеш torrent
            string keydownload = $"kinozal:parseMagnet:download:{id}";
            if (Startup.memoryCache.TryGetValue(keydownload, out byte[] _t))
                return File(_t, "application/x-bittorrent");

            string keymagnet = $"kinozal:parseMagnet:{id}";
            if (Startup.memoryCache.TryGetValue(keymagnet, out string _m))
                return Redirect(_m);
            #endregion

            #region emptycache
            string keyerror = $"kinozal:parseMagnet:{id}:error";
            if (Startup.memoryCache.TryGetValue(keyerror, out _))
            {
                if (TorrentCache.Read(keydownload) is var tcache && tcache.cache)
                    return File(tcache.torrent, "application/x-bittorrent");

                if (TorrentCache.ReadMagnet(keymagnet) is var mcache && mcache.cache)
                    Redirect(mcache.torrent);

                return Content("error");
            }
            #endregion

            #region Download
            if (Cookie != null)
            {
                _t = await HttpClient.Download("http://dl.kinozal.tv/download.php?id=" + id, cookie: Cookie, referer: jackett.Kinozal.host, timeoutSeconds: 10);
                if (_t != null && BencodeTo.Magnet(_t) != null)
                {
                    if (jackett.cache)
                    {
                        TorrentCache.Write(keydownload, _t);
                        Startup.memoryCache.Set(keydownload, _t, DateTime.Now.AddMinutes(Math.Max(1, jackett.torrentCacheToMinutes)));
                    }

                    return File(_t, "application/x-bittorrent");
                }
            }
            #endregion

            var proxyManager = new ProxyManager("kinozal", jackett.Kinozal);

            #region Инфо хеш
            string srv_details = await HttpClient.Post($"{jackett.Kinozal.host}/get_srv_details.php?id={id}&action=2", $"id={id}&action=2", "__cfduid=d476ac2d9b5e18f2b67707b47ebd9b8cd1560164391; uid=20520283; pass=ouV5FJdFCd;", proxy: proxyManager.Get(), timeoutSeconds: 10);
            if (srv_details != null)
            {
                string torrentHash = new Regex("<ul><li>Инфо хеш: +([^<]+)</li>").Match(srv_details).Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(torrentHash))
                {
                    string magnet = $"magnet:?xt=urn:btih:{torrentHash}";

                    if (jackett.cache)
                    {
                        TorrentCache.Write(keymagnet, magnet);
                        Startup.memoryCache.Set(keymagnet, magnet, DateTime.Now.AddMinutes(Math.Max(1, jackett.torrentCacheToMinutes)));
                    }

                    return Redirect(magnet);
                }
            }
            #endregion

            if (jackett.emptycache && jackett.cache)
                Startup.memoryCache.Set(keyerror, 0, DateTime.Now.AddMinutes(1));

            if (jackett.cache)
            {
                if (TorrentCache.Read(keydownload) is var tcache && tcache.cache)
                    return File(tcache.torrent, "application/x-bittorrent");

                if (TorrentCache.ReadMagnet(keymagnet) is var mcache && mcache.cache)
                    Redirect(mcache.torrent);
            }

            proxyManager.Refresh();
            return Content("error");
        }
        #endregion


        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query, string[] cats)
        {
            if (!jackett.Kinozal.enable)
                return Task.FromResult(false);

            return JackettCache.Invoke($"kinozal:{string.Join(":", cats ?? new string[] { })}:{query}", torrents, () => parsePage(host, query, cats));
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query, string[] cats)
        {
            var torrents = new List<TorrentDetails>();

            #region Кеш html
            var proxyManager = new ProxyManager("kinozal", jackett.Kinozal);

            string html = await HttpClient.Get($"{jackett.Kinozal.host}/browse.php?s={HttpUtility.UrlEncode(query)}&g=0&c=0&v=0&d=0&w=0&t=0&f=0", proxy: proxyManager.Get(), timeoutSeconds: jackett.timeoutSeconds);

            if (html != null && html.Contains("Кинозал.ТВ</title>"))
            {
                if (!html.Contains(">Выход</a>") && !string.IsNullOrWhiteSpace(jackett.Kinozal.login.u) && !string.IsNullOrWhiteSpace(jackett.Kinozal.login.p))
                    TakeLogin();
            }
            else if (html == null)
            {
                proxyManager.Refresh();
                return null;
            }
            #endregion

            foreach (string row in Regex.Split(html, "<tr class=('first bg'|bg)>").Skip(1))
            {
                #region Локальный метод - Match
                string Match(string pattern, int index = 1)
                {
                    string res = HttpUtility.HtmlDecode(new Regex(pattern, RegexOptions.IgnoreCase).Match(row).Groups[index].Value.Trim());
                    res = Regex.Replace(res, "[\n\r\t ]+", " ");
                    return res.Trim();
                }
                #endregion

                if (string.IsNullOrWhiteSpace(row))
                    continue;

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
                    createTime = tParse.ParseCreateTime(Match("<td class='s'>([0-9]{2}.[0-9]{2}.[0-9]{4}) в [0-9]{2}:[0-9]{2}</td>"), "dd.MM.yyyy");
                }

                //if (createTime == default)
                //    continue;
                #endregion

                #region Данные раздачи
                string url = Match("href=\"/(details.php\\?id=[0-9]+)\"");
                string tracker = Match("src=\"/pic/cat/([0-9]+)\\.gif\"");
                string title = Match("class=\"r[0-9]+\">([^<]+)</a>");
                string _sid = Match("<td class='sl_s'>([0-9]+)</td>");
                string _pir = Match("<td class='sl_p'>([0-9]+)</td>");
                string sizeName = Match("<td class='s'>([0-9\\.,]+ (МБ|ГБ))</td>");

                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(tracker))
                    continue;

                url = $"{jackett.Kinozal.host}/{url}";
                #endregion

                #region Парсим раздачи
                int relased = 0;
                string name = null, originalname = null;

                if (tracker is "1002" or "8" or "6" or "15" or "17" or "35" or "39" or "13" or "14" or "24" or "11" or "10" or "9" or "47" or "18" or "37" or "12" or "7" or "16")
                {
                    #region Фильмы
                    // Бэд трип (Приколисты в дороге) / Bad Trip / 2020 / ДБ, СТ / WEB-DLRip (AVC)
                    // Успеть всё за месяц / 30 jours max / 2020 / ЛМ / WEB-DLRip
                    var g = Regex.Match(title, "^([^\\(/]+) (\\([^\\)/]+\\) )?/ ([^\\(/]+) / ([0-9]{4})").Groups;
                    if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[3].Value) && !string.IsNullOrWhiteSpace(g[4].Value))
                    {
                        name = g[1].Value;
                        originalname = g[3].Value;

                        if (int.TryParse(g[4].Value, out int _yer))
                            relased = _yer;
                    }
                    else
                    {
                        // Голая правда / 2020 / ЛМ / WEB-DLRip
                        g = Regex.Match(title, "^([^/\\(]+) / ([0-9]{4})").Groups;

                        name = g[1].Value;
                        if (int.TryParse(g[2].Value, out int _yer))
                            relased = _yer;
                    }
                    #endregion
                }
                else if (tracker == "45" || tracker == "22")
                {
                    #region Сериал - Русский
                    if (row.Contains("сезон"))
                    {
                        // Сельский детектив (6 сезон: 1-2 серии из 2) ([^/]+)?/ 2020 / РУ / WEB-DLRip (AVC)
                        // Любовь в рабочие недели (1 сезон: 1 серия из 15) / 2020 / РУ / WEB-DLRip (AVC)
                        // Фитнес (Королева фитнеса) (1-4 сезон: 1-80 серии из 80) / 2018-2020 / РУ / WEB-DLRip
                        // Бывшие (1-3 сезон: 1-24 серии из 24) / 2016-2020 / РУ / WEB-DLRip (AVC)
                        var g = Regex.Match(title, "^([^\\(/]+) (\\([^\\)/]+\\) )?\\([0-9\\-]+ сезоны?: [^\\)/]+\\) ([^/]+ )?/ ([0-9]{4})").Groups;
                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[4].Value))
                        {
                            name = g[1].Value;

                            if (int.TryParse(g[4].Value, out int _yer))
                                relased = _yer;
                        }
                    }
                    else
                    {
                        // Авантюра на двоих (1-8 серии из 8) / 2021 / РУ /  WEBRip (AVC)
                        // Жизнь после жизни (Небеса подождут) (1-16 серии из 16) / 2016 / РУ / WEB-DLRip
                        var g = Regex.Match(title, "^([^\\(/]+) (\\([^\\)/]+\\) )?\\([^\\)/]+\\) ([^/]+ )?/ ([0-9]{4})").Groups;

                        name = g[1].Value;
                        if (int.TryParse(g[4].Value, out int _yer))
                            relased = _yer;
                    }
                    #endregion
                }
                else if (tracker == "46" || tracker == "21" || tracker == "20")
                {
                    #region Сериал - Буржуйский
                    if (row.Contains("сезон"))
                    {
                        // Сокол и Зимний солдат (1 сезон: 1-2 серия из 6) / The Falcon and the Winter Soldier / 2021 / ЛД (#NW), СТ / WEB-DL (1080p)
                        // Голубая кровь (Семейная традиция) (11 сезон: 1-9 серия из 20) / Blue Bloods / 2020 / ПМ (BaibaKo) / WEBRip
                        var g = Regex.Match(title, "^([^\\(/]+) (\\([^\\)/]+\\) )?\\([0-9\\-]+ сезоны?: [^\\)/]+\\) ([^/]+ )?/ ([^\\(/]+) / ([0-9]{4})").Groups;
                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[4].Value) && !string.IsNullOrWhiteSpace(g[5].Value))
                        {
                            name = g[1].Value;
                            originalname = g[4].Value;

                            if (int.TryParse(g[5].Value, out int _yer))
                                relased = _yer;
                        }
                    }
                    else
                    {
                        // Дикий ангел (151-270 серии из 270) / Muneca Brava / 1998-1999 / ПМ / DVB
                        var g = Regex.Match(title, "^([^\\(/]+) (\\([^\\)/]+\\) )?\\([^\\)/]+\\) ([^/]+ )?/ ([^\\(/]+) / ([0-9]{4})").Groups;
                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[4].Value) && !string.IsNullOrWhiteSpace(g[5].Value))
                        {
                            name = g[1].Value;
                            originalname = g[4].Value;

                            if (int.TryParse(g[5].Value, out int _yer))
                                relased = _yer;
                        }
                        else
                        {
                            g = Regex.Match(title, "^([^\\(/]+) / ([^\\(/]+) / ([0-9]{4})").Groups;
                            name = g[1].Value;
                            originalname = g[2].Value;

                            if (int.TryParse(g[3].Value, out int _yer))
                                relased = _yer;
                        }
                    }
                    #endregion
                }
                else if (tracker is "1006" or "48" or "49" or "50" or "38")
                {
                    #region ТВ-шоу
                    // Топ Гир (30 сезон: 1-2 выпуски из 10) / Top Gear / 2021 / ЛМ (ColdFilm) / WEBRip
                    var g = Regex.Match(title, "^([^\\(/]+) (\\([^\\)/]+\\) )?/ ([^\\(/]+) / ([0-9]{4})").Groups;
                    if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[3].Value) && !string.IsNullOrWhiteSpace(g[4].Value))
                    {
                        name = g[1].Value;
                        originalname = g[3].Value;

                        if (int.TryParse(g[4].Value, out int _yer))
                            relased = _yer;
                    }
                    else
                    {
                        // Супермама (3 сезон: 1-12 выпуски из 40) / 2021 / РУ / IPTV (1080p)
                        g = Regex.Match(title, "^([^/\\(]+) (\\([^\\)/]+\\) )?/ ([0-9]{4})").Groups;

                        name = g[1].Value;
                        if (int.TryParse(g[3].Value, out int _yer))
                            relased = _yer;
                    }
                    #endregion
                }
                #endregion

                if (string.IsNullOrWhiteSpace(name))
                    name = Regex.Split(title, "(\\[|\\/|\\(|\\|)", RegexOptions.IgnoreCase)[0].Trim();

                if (!string.IsNullOrWhiteSpace(name) || cats == null)
                {
                    // Id новости
                    string id = Match("href=\"/details.php\\?id=([0-9]+)\"");
                    if (string.IsNullOrWhiteSpace(id))
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

                    if (types == null)
                        continue;

                    if (cats != null)
                    {
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
                        trackerName = "kinozal",
                        types = types,
                        url = url,
                        title = title,
                        sid = sid,
                        pir = pir,
                        sizeName = sizeName,
                        createTime = createTime,
                        parselink = $"{host}/kinozal/parsemagnet?id={id}",
                        name = name,
                        originalname = originalname,
                        relased = relased
                    });
                }
            }

            return torrents;
        }
        #endregion
    }
}

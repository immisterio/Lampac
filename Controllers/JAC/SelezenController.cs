using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Engine.Parse;
using Lampac.Models.JAC;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Controllers.JAC
{
    [Route("selezen/[action]")]
    public class SelezenController : BaseController
    {
        #region Cookie / TakeLogin
        static string Cookie(IMemoryCache memoryCache)
        {
            if (memoryCache.TryGetValue("cron:SelezenController:Cookie", out string cookie))
                return cookie;

            return null;
        }

        async static Task<bool> TakeLogin(IMemoryCache memoryCache)
        {
            try
            {
                var clientHandler = new System.Net.Http.HttpClientHandler()
                {
                    AllowAutoRedirect = false
                };

                clientHandler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
                using (var client = new System.Net.Http.HttpClient(clientHandler))
                {
                    client.Timeout = TimeSpan.FromSeconds(AppInit.conf.timeoutSeconds);
                    client.MaxResponseContentBufferSize = 2000000; // 2MB
                    client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/75.0.3770.100 Safari/537.36");

                    var postParams = new Dictionary<string, string>();
                    postParams.Add("login_name", AppInit.conf.Selezen.login.u);
                    postParams.Add("login_password", AppInit.conf.Selezen.login.p);
                    postParams.Add("login_not_save", "1");
                    postParams.Add("login", "submit");

                    using (var postContent = new System.Net.Http.FormUrlEncodedContent(postParams))
                    {
                        using (var response = await client.PostAsync(AppInit.conf.Selezen.host, postContent))
                        {
                            if (response.Headers.TryGetValues("Set-Cookie", out var cook))
                            {
                                string PHPSESSID = null;
                                foreach (string line in cook)
                                {
                                    if (string.IsNullOrWhiteSpace(line))
                                        continue;

                                    if (line.Contains("PHPSESSID="))
                                        PHPSESSID = new Regex("PHPSESSID=([^;]+)(;|$)").Match(line).Groups[1].Value;
                                }

                                if (!string.IsNullOrWhiteSpace(PHPSESSID))
                                {
                                    memoryCache.Set("cron:SelezenController:Cookie", $"PHPSESSID={PHPSESSID}; _ym_isad=2;", TimeSpan.FromHours(1));
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return false;
        }
        #endregion


        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string url)
        {
            string key = $"selezen:parseMagnet:{url}";
            if (Startup.memoryCache.TryGetValue(key, out string _m))
                return Redirect(_m);

            #region Авторизация
            if (Cookie(Startup.memoryCache) == null)
            {
                string authKey = "selezen:TakeLogin()";
                if (Startup.memoryCache.TryGetValue(authKey, out _))
                    return Content("TakeLogin == false");

                if (await TakeLogin(Startup.memoryCache) == false)
                {
                    if (TorrentCache.Read(key, out _m))
                        Redirect(_m);

                    Startup.memoryCache.Set(authKey, 0, TimeSpan.FromMinutes(1));
                    return Content("TakeLogin == false");
                }
            }
            #endregion

            string html = await HttpClient.Get(url, cookie: Cookie(Startup.memoryCache), timeoutSeconds: 10);
            if (html == null)
            {
                if (TorrentCache.Read(key, out _m))
                    Redirect(_m);

                return Content("error");
            }

            string magnet = new Regex("href=\"(magnet:[^\"]+)\"").Match(html).Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(magnet))
            {
                TorrentCache.Write(key, magnet);
                Startup.memoryCache.Set(key, magnet, DateTime.Now.AddMinutes(AppInit.conf.magnetCacheToMinutes));
                return Redirect(magnet);
            }

            if (TorrentCache.Read(key, out _m))
                Redirect(_m);

            return Content("error");
        }
        #endregion

        #region parsePage
        async public static Task<bool> parsePage(string host, ConcurrentBag<TorrentDetails> torrents, string query)
        {
            if (!AppInit.conf.Selezen.enable)
                return false;

            #region Кеш
            string cachekey = $"selezen:{query}";
            if (!HtmlCache.Read(cachekey, out string cachehtml))
            {
                string html = await HttpClient.Post($"{AppInit.conf.Selezen.host}/index.php?do=search", $"do=search&subaction=search&search_start=0&full_search=1&result_from=1&story={HttpUtility.UrlEncode(query)}&titleonly=0&searchuser=&replyless=0&replylimit=0&searchdate=0&beforeafter=after&sortby=date&resorder=desc&showposts=0&catlist%5B%5D=9", cookie: Cookie(Startup.memoryCache), timeoutSeconds: AppInit.conf.timeoutSeconds);

                if (html != null && html.Contains("релизы от селезнь</title>"))
                {
                    cachehtml = html;
                    HtmlCache.Write(cachekey, html);
                }

                if (cachehtml == null)
                    return false;
            }
            #endregion

            #region Авторизация
            if (Cookie(Startup.memoryCache) == null)
            {
                string authKey = "selezen:TakeLogin()";
                if (Startup.memoryCache.TryGetValue(authKey, out _))
                    return false;

                if (await TakeLogin(Startup.memoryCache) == false)
                {
                    Startup.memoryCache.Set(authKey, 0, TimeSpan.FromMinutes(1));
                    return false;
                }
            }
            #endregion

            foreach (string row in cachehtml.Split("class=\"card radius-10 overflow-hidden\"").Skip(1))
            {
                if (row.Contains(">Аниме</a>") || row.Contains(" [S0"))
                    continue;

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

                #region Данные раздачи
                var g = Regex.Match(row, "<a href=\"(https?://[^<]+)\"><h4 class=\"card-title\">([^<]+)</h4>").Groups;
                string url = g[1].Value;
                string title = g[2].Value;

                string _sid = Match("<i class=\"bx bx-chevrons-up\"></i>([0-9 ]+)").Trim();
                string _pir = Match("<i class=\"bx bx-chevrons-down\"></i>([0-9 ]+)").Trim();
                string sizeName = Match("<span class=\"bx bx-download\"></span>([^<]+)</a>").Trim();
                DateTime createTime = tParse.ParseCreateTime(Match("class=\"bx bx-calendar\"></span> ?([0-9]{2}\\.[0-9]{2}\\.[0-9]{4} [0-9]{2}:[0-9]{2})</a>"), "dd.MM.yyyy HH:mm");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
                    continue;
                #endregion

                #region Парсим раздачи
                int relased = 0;
                string name = null, originalname = null;

                // Бэд трип / Приколисты в дороге / Bad Trip (2020)
                g = Regex.Match(title, "^([^/\\(]+) / [^/]+ / ([^/\\(]+) \\(([0-9]{4})\\)").Groups;
                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                {
                    name = g[1].Value;
                    originalname = g[2].Value;

                    if (int.TryParse(g[3].Value, out int _yer))
                        relased = _yer;
                }
                else
                {
                    // Летний лагерь / A Week Away (2021)
                    g = Regex.Match(title, "^([^/\\(]+) / ([^/\\(]+) \\(([0-9]{4})\\)").Groups;
                    name = g[1].Value;
                    originalname = g[2].Value;

                    if (int.TryParse(g[3].Value, out int _yer))
                        relased = _yer;
                }
                #endregion

                if (!string.IsNullOrWhiteSpace(name))
                {
                    #region types
                    string[] types = new string[] { "movie" };
                    if (row.Contains(">Мульт") || row.Contains(">мульт"))
                        types = new string[] { "multfilm" };
                    #endregion

                    int.TryParse(_sid, out int sid);
                    int.TryParse(_pir, out int pir);

                    torrents.Add(new TorrentDetails()
                    {
                        trackerName = "selezen",
                        types = types,
                        url = url,
                        title = title,
                        sid = sid,
                        pir = pir,
                        sizeName = sizeName,
                        createTime = createTime,
                        parselink = $"{host}/selezen/parsemagnet?url={HttpUtility.UrlEncode(url)}",
                        name = name,
                        originalname = originalname,
                        relased = relased

                    });
                }
            }

            return true;
        }
        #endregion
    }
}

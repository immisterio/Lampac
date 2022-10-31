using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Lampac.Engine.CORE;
using Lampac.Engine.Parse;
using Lampac.Engine;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using Lampac.Models.JAC;

namespace Lampac.Controllers.JAC
{
    //[Route("underverse/[action]")]
    public class UnderverseController : BaseController
    {
        #region Cookie / TakeLogin
        static string Cookie;

        async public static Task<bool> TakeLogin()
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
                    client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.51 Safari/537.36");

                    client.DefaultRequestHeaders.Add("cache-control", "no-cache");
                    client.DefaultRequestHeaders.Add("dnt", "1");
                    client.DefaultRequestHeaders.Add("origin", AppInit.conf.Underverse.host);
                    client.DefaultRequestHeaders.Add("pragma", "no-cache");
                    client.DefaultRequestHeaders.Add("referer", $"{AppInit.conf.Underverse.host}/");
                    client.DefaultRequestHeaders.Add("sec-ch-ua", "\" Not A;Brand\";v=\"99\", \"Chromium\";v=\"99\", \"Google Chrome\";v=\"99\"");
                    client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                    client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
                    client.DefaultRequestHeaders.Add("sec-fetch-dest", "document");
                    client.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
                    client.DefaultRequestHeaders.Add("sec-fetch-site", "same-origi");
                    client.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
                    client.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");

                    var postParams = new Dictionary<string, string>();
                    postParams.Add("login_username", AppInit.conf.Underverse.login.u);
                    postParams.Add("login_password", AppInit.conf.Underverse.login.p);
                    postParams.Add("autologin", "1");
                    postParams.Add("login", "Вход");

                    using (var postContent = new System.Net.Http.FormUrlEncodedContent(postParams))
                    {
                        using (var response = await client.PostAsync($"{AppInit.conf.Underverse.host}/login.php", postContent))
                        {
                            if (response.Headers.TryGetValues("Set-Cookie", out var cook))
                            {
                                string bbdata = null;
                                foreach (string line in cook)
                                {
                                    if (string.IsNullOrWhiteSpace(line))
                                        continue;

                                    if (line.Contains("bb_data="))
                                        bbdata = new Regex("bb_data=([^;]+)(;|$)").Match(line).Groups[1].Value;
                                }

                                if (!string.IsNullOrWhiteSpace(bbdata))
                                {
                                    Cookie = $"bb_data={bbdata}; _ym_isad=2;";
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

        #region getHtml
        async static Task<string> getHtml(string query)
        {
            try
            {
                var handler = new System.Net.Http.HttpClientHandler()
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.Brotli | System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                };
                handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

                using (var client = new System.Net.Http.HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(AppInit.conf.timeoutSeconds);
                    client.DefaultRequestHeaders.Add("cookie", Cookie);

                    using (var response = await client.GetAsync($"{AppInit.conf.Underverse.host}/tracker.php?nm=" + HttpUtility.UrlEncode(query)))
                    {
                        using (var content = response.Content)
                        {
                            string res = System.Text.Encoding.GetEncoding(1251).GetString(await content.ReadAsByteArrayAsync());
                            if (string.IsNullOrWhiteSpace(res))
                                return null;

                            return res;
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }
        #endregion


        #region parsePage
        async public static Task<bool> parsePage(ConcurrentBag<TorrentDetails> torrents, string query, string[] cats)
        {
            if (!AppInit.conf.Underverse.enable)
                return false;

            #region Авторизация
            if (Cookie == null)
            {
                string authKey = "underverse:TakeLogin()";
                if (Startup.memoryCache.TryGetValue(authKey, out _))
                    return false;

                if (await TakeLogin() == false)
                {
                    Startup.memoryCache.Set(authKey, 0, TimeSpan.FromMinutes(1));
                    return false;
                }
            }
            #endregion

            #region Кеш
            string cachekey = $"underverse:{string.Join(":", cats ?? new string[] { })}:{query}";
            var cread = await HtmlCache.Read(cachekey);

            if (cread.emptycache)
                return false;

            if (!cread.cache)
            {
                string html = await getHtml(query);
                if (html != null)
                {
                    cread.html = html;
                    await HtmlCache.Write(cachekey, html);
                }

                if (cread.html == null)
                {
                    HtmlCache.EmptyCache(cachekey);
                    return false;
                }
            }
            #endregion

            foreach (string row in cread.html.Split("id=\"tor_").Skip(1))
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
                string url = Match("href=\"[^\"]+/(viewtopic.php\\?t=[0-9]+)\"");
                string tracker = Match("href=\"tracker.php\\?f=([0-9]+)");
                string magnet = Match("href=\"(magnet:[^\"]+)\"");
                DateTime createTime = tParse.ParseCreateTime(Match("<p>([0-9]{2}-[^-<]+-[0-9]{2})</p>").Replace("-", " "), "dd.MM.yy");
                string title = Match("href=\"[^\"]+/viewtopic.php\\?t=[0-9]+\"><b>([^<]+)</b>");
                string _sid = Match("class=\"row4 seedmed\" [^>]+><b>([0-9]+)</b>");
                string _pir = Match("class=\"row4 leechmed\" [^>]+><b>([0-9]+)</b>");
                string sizeName = Match("href=\"[^\"]+/download.php\\?id=[^\"]+\">([^<]+)</a>").Replace("&nbsp;", " ");

                if (string.IsNullOrWhiteSpace(magnet) || string.IsNullOrWhiteSpace(title))
                    continue;

                url = $"{AppInit.conf.Underverse.host}/{url}";
                #endregion

                #region Парсим раздачи
                int relased = 0;
                string name = null, originalname = null;

                if (tracker is "99" or "100" or "16" or "1023" or "1024" or "106" or "105")
                {
                    #region Фильмы
                    // Осторожно, Кенгуру! / Хроники кенгуру / Die Kanguru-Chroniken (Дани Леви / Dani Levy) [2020 г., комедия, BDRemux 1080p] Dub (iTunes) + Original (Ger)
                    var g = Regex.Match(title, "^([^/\\(\\[]+) / [^/\\(\\[]+ / ([^/\\(\\[]+) \\([^\\)]+\\) \\[([0-9]{4})").Groups;
                    if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                    {
                        name = g[1].Value;
                        originalname = g[2].Value;

                        if (int.TryParse(g[3].Value, out int _yer))
                            relased = _yer;
                    }
                    else
                    {
                        // Звонок. Последняя глава / Sadako (Хидэо Наката / Hideo Nakata) [2019 г., ужасы, BDRemux 1080p] Dub (iTunes) + Original (Jap)
                        g = Regex.Match(title, "^([^/\\(\\[]+) / ([^/\\(\\[]+) \\([^\\)]+\\) \\[([0-9]{4})").Groups;
                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                        {
                            name = g[1].Value;
                            originalname = g[2].Value;

                            if (int.TryParse(g[3].Value, out int _yer))
                                relased = _yer;
                        }
                        else
                        {
                            // Дневной дозор (Тимур Бекмамбетов) [2006 г., Россия, боевик, триллер, фэнтези, BDRip-AVC]
                            g = Regex.Match(title, "^([^/\\(\\[]+) \\([^\\)]+\\) \\[([0-9]{4})").Groups;
                            if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                            {
                                name = g[1].Value;
                                if (int.TryParse(g[2].Value, out int _yer))
                                    relased = _yer;
                            }
                        }
                    }
                    #endregion
                }
                else if (tracker is "1018")
                {
                    #region Сериалы
                    name = Regex.Match(title, "^([^/\\(\\[]+) ").Groups[1].Value;
                    if (Regex.IsMatch(name ?? "", "(Сезон|Серии)", RegexOptions.IgnoreCase))
                        continue;

                    originalname = Regex.Match(title, "^[^/\\(\\[]+ / ([^/\\(\\[]+)").Groups[1].Value;
                    if (Regex.IsMatch(originalname, "[а-яА-Я]"))
                    {
                        originalname = Regex.Match(title, "^[^/\\(\\[]+ / [^/\\(\\[]+ / ([^/\\(\\[]+)").Groups[1].Value;
                        if (Regex.IsMatch(originalname, "[а-яА-Я]"))
                            originalname = null;
                    }

                    if (string.IsNullOrWhiteSpace(originalname))
                        originalname = null;

                    if (int.TryParse(Regex.Match(title, " \\[([0-9]{4})(г|,|-| )").Groups[1].Value, out int _yer))
                        relased = _yer;
                    #endregion
                }
                else if (tracker is "113" or "114" or "78" or "81" or "82" or "59" or "60" or "62" or "64" or "1019")
                {
                    #region Нестандартные титлы
                    name = Regex.Match(title, "^([^/\\(\\[]+) ").Groups[1].Value;

                    if (int.TryParse(Regex.Match(title, " \\[([0-9]{4})(,|-| )").Groups[1].Value, out int _yer))
                        relased = _yer;

                    if (Regex.IsMatch(name ?? "", "(Сезон|Серии)", RegexOptions.IgnoreCase))
                        continue;
                    #endregion
                }
                #endregion

                if (!string.IsNullOrWhiteSpace(name) || cats == null)
                {
                    #region types
                    string[] types = null;
                    switch (tracker)
                    {
                        case "99":
                        case "100":
                        case "16":
                        case "106":
                        case "105":
                            types = new string[] { "movie" };
                            break;
                        case "1019":
                        case "1018":
                            types = new string[] { "serial" };
                            break;
                        case "1023":
                        case "1024":
                            types = new string[] { "multfilm" };
                            break;
                        case "113":
                        case "114":
                            types = new string[] { "documovie" };
                            break;
                        case "78":
                        case "81":
                        case "82":
                            types = new string[] { "tvshow" };
                            break;
                        case "59":
                        case "60":
                        case "62":
                        case "64":
                            types = new string[] { "docuserial", "documovie" };
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
                        trackerName = "underverse",
                        types = types,
                        url = url,
                        title = title,
                        sid = sid,
                        pir = pir,
                        sizeName = sizeName,
                        createTime = createTime,
                        magnet = magnet,
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

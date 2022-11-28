using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Engine.Parse;
using Lampac.Engine;
using System.Collections.Concurrent;
using Lampac.Models.JAC;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Controllers.JAC
{
    [Route("megapeer/[action]")]
    public class MegapeerController : BaseController
    {
        #region parseMagnet
        static string TorrentFileMemKey(string id) => $"megapeer:parseMagnet:{id}";

        async public Task<ActionResult> parseMagnet(string id, bool usecache)
        {
            if (!AppInit.conf.Megapeer.enable)
                return Content("disable");

            string key = TorrentFileMemKey(id);
            if (Startup.memoryCache.TryGetValue(key, out byte[] _m))
                return File(_m, "application/x-bittorrent");

            if (usecache || Startup.memoryCache.TryGetValue($"{key}:error", out _))
            {
                if (await TorrentCache.Read(key) is var tc && tc.cache)
                    return File(tc.torrent, "application/x-bittorrent");

                return Content("error");
            }

            byte[] _t = await HttpClient.Download($"{AppInit.conf.Megapeer.host}/download/{id}", referer: AppInit.conf.Megapeer.host, timeoutSeconds: 10);
            if (_t != null && BencodeTo.Magnet(_t) != null)
            {
                await TorrentCache.Write(key, _t);
                Startup.memoryCache.Set(key, _t, DateTime.Now.AddMinutes(AppInit.conf.jac.torrentCacheToMinutes));
                return File(_t, "application/x-bittorrent");
            }
            else if (AppInit.conf.jac.emptycache)
                Startup.memoryCache.Set($"{key}:error", 0, DateTime.Now.AddMinutes(AppInit.conf.jac.torrentCacheToMinutes));

            if (await TorrentCache.Read(key) is var tcache && tcache.cache)
                return File(tcache.torrent, "application/x-bittorrent");

            return Content("error");
        }
        #endregion

        async public static Task<bool> parsePage(string host, ConcurrentBag<TorrentDetails> torrents, string query, string cat)
        {
            if (!AppInit.conf.Megapeer.enable)
                return false;

            #region Кеш html
            string cachekey = $"megapeer:{cat}:{query}";
            var cread = await HtmlCache.Read(cachekey);
            bool validrq = cread.cache;

            if (cread.emptycache)
                return false;

            if (!cread.cache)
            {
                string html = await HttpClient.Get($"{AppInit.conf.Megapeer.host}/browse.php?search={HttpUtility.UrlEncode(query)}&cat={cat}", encoding: Encoding.GetEncoding(1251), useproxy: AppInit.conf.Megapeer.useproxy, timeoutSeconds: AppInit.conf.jac.timeoutSeconds);

                if (html != null && html.Contains("id=\"logo\""))
                {
                    cread.html = html;
                    await HtmlCache.Write(cachekey, html);
                    validrq = true;
                }

                if (cread.html == null)
                {
                    HtmlCache.EmptyCache(cachekey);
                    return false;
                }
            }
            #endregion

            foreach (string row in cread.html.Split("class=\"tCenter hl-tr\"").Skip(1))
            {
                #region Локальный метод - Match
                string Match(string pattern, int index = 1)
                {
                    string res = HttpUtility.HtmlDecode(new Regex(pattern, RegexOptions.IgnoreCase).Match(row).Groups[index].Value.Trim());
                    res = Regex.Replace(res, "[\n\r\t ]+", " ");
                    return res.Replace(" ", " ").Trim(); // Меняем непонятный символ похожий на проблел, на обычный проблел
                }
                #endregion

                #region createTime
                DateTime createTime = tParse.ParseCreateTime(Match("<span>Добавлен:</span> ([0-9]+ [^ ]+ [0-9]+)"), "dd.MM.yyyy");
                //if (createTime == default)
                //    continue;
                #endregion

                #region Данные раздачи
                string url = Match("href=\"/(torrent/[0-9]+)\"");
                string title = Match("class=\"med tLink hl-tags bold\" [^>]+>([^\n\r]+)</a>");
                title = Regex.Replace(title, "<[^>]+>", "");

                string sizeName = Match("href=\"download/[0-9]+\">([\n\r\t ]+)?([^<\n\r]+)<", 2).Trim();
                string downloadid = Match("href=\"/?download/([0-9]+)\"");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(downloadid))
                    continue;

                url = $"{AppInit.conf.Megapeer.host}/{url}";
                #endregion

                #region Парсим раздачи
                int relased = 0;
                string name = null, originalname = null;

                if (cat == "174")
                {
                    #region Зарубежные фильмы
                    var g = Regex.Match(title, "^([^/]+) / ([^/]+) / ([^/\\(]+) \\(([0-9]{4})\\)").Groups;
                    if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                    {
                        name = g[1].Value;
                        originalname = g[3].Value;

                        if (int.TryParse(g[4].Value, out int _yer))
                            relased = _yer;
                    }
                    else
                    {
                        g = Regex.Match(title, "^([^/\\(]+) / ([^/\\(]+) \\(([0-9]{4})\\)").Groups;

                        name = g[1].Value;
                        originalname = g[2].Value;

                        if (int.TryParse(g[3].Value, out int _yer))
                            relased = _yer;
                    }
                    #endregion
                }
                else if (cat == "79")
                {
                    #region Наши фильмы
                    var g = Regex.Match(title, "^([^/\\(]+) \\(([0-9]{4})\\)").Groups;
                    name = g[1].Value;

                    if (int.TryParse(g[2].Value, out int _yer))
                        relased = _yer;
                    #endregion
                }
                else if (cat == "6")
                {
                    #region Зарубежные сериалы
                    var g = Regex.Match(title, "^([^/]+) / [^/]+ / [^/]+ / ([^/\\[]+) \\[[^\\]]+\\] +\\(([0-9]{4})(\\)|-)").Groups;
                    if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                    {
                        name = g[1].Value;
                        originalname = g[2].Value;

                        if (int.TryParse(g[3].Value, out int _yer))
                            relased = _yer;
                    }
                    else
                    {
                        g = Regex.Match(title, "^([^/]+) / [^/]+ / ([^/\\[]+) \\[[^\\]]+\\] +\\(([0-9]{4})(\\)|-)").Groups;
                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                        {
                            name = g[1].Value;
                            originalname = g[2].Value;

                            if (int.TryParse(g[3].Value, out int _yer))
                                relased = _yer;
                        }
                        else
                        {
                            g = Regex.Match(title, "^([^/]+) / ([^/\\[]+) \\[[^\\]]+\\] +\\(([0-9]{4})(\\)|-)").Groups;

                            name = g[1].Value;
                            originalname = g[2].Value;

                            if (int.TryParse(g[3].Value, out int _yer))
                                relased = _yer;
                        }
                    }
                    #endregion
                }
                else if (cat == "5")
                {
                    #region Наши сериалы
                    var g = Regex.Match(title, "^([^/]+) \\[[^\\]]+\\] \\(([0-9]{4})(\\)|-)").Groups;
                    name = g[1].Value;

                    if (int.TryParse(g[2].Value, out int _yer))
                        relased = _yer;
                    #endregion
                }
                else if (cat == "55" || cat == "57" || cat == "76")
                {
                    #region Научно-популярные фильмы / Телевизор / Мультипликация
                    if (title.Contains(" / "))
                    {
                        if (title.Contains("[") && title.Contains("]"))
                        {
                            var g = Regex.Match(title, "^([^/]+) / ([^/]+) / ([^/\\[]+) \\[[^\\]]+\\] +\\(([0-9]{4})(\\)|-)").Groups;
                            if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                            {
                                name = g[1].Value;
                                originalname = g[3].Value;

                                if (int.TryParse(g[4].Value, out int _yer))
                                    relased = _yer;
                            }
                            else
                            {
                                g = Regex.Match(title, "^([^/]+) / ([^/\\[]+) \\[[^\\]]+\\] +\\(([0-9]{4})(\\)|-)").Groups;

                                name = g[1].Value;
                                originalname = g[2].Value;

                                if (int.TryParse(g[3].Value, out int _yer))
                                    relased = _yer;
                            }
                        }
                        else
                        {
                            var g = Regex.Match(title, "^([^/]+) / ([^/]+) / ([^/\\(]+) \\(([0-9]{4})\\)").Groups;
                            if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                            {
                                name = g[1].Value;
                                originalname = g[3].Value;

                                if (int.TryParse(g[4].Value, out int _yer))
                                    relased = _yer;
                            }
                            else
                            {
                                g = Regex.Match(title, "^([^/\\(]+) / ([^/\\(]+) \\(([0-9]{4})\\)").Groups;

                                name = g[1].Value;
                                originalname = g[2].Value;

                                if (int.TryParse(g[3].Value, out int _yer))
                                    relased = _yer;
                            }
                        }
                    }
                    else
                    {
                        if (title.Contains("[") && title.Contains("]"))
                        {
                            var g = Regex.Match(title, "^([^/\\[]+) \\[[^\\]]+\\] +\\(([0-9]{4})(\\)|-)").Groups;
                            name = g[1].Value;

                            if (int.TryParse(g[2].Value, out int _yer))
                                relased = _yer;
                        }
                        else
                        {
                            var g = Regex.Match(title, "^([^/\\(]+) \\(([0-9]{4})\\)").Groups;
                            name = g[1].Value;

                            if (int.TryParse(g[2].Value, out int _yer))
                                relased = _yer;
                        }
                    }
                    #endregion
                }
                #endregion

                if (!string.IsNullOrWhiteSpace(name) || cat == "0")
                {
                    #region types
                    string[] types = new string[] { };
                    switch (cat)
                    {
                        case "174":
                        case "79":
                            types = new string[] { "movie" };
                            break;
                        case "6":
                        case "5":
                            types = new string[] { "serial" };
                            break;
                        case "55":
                            types = new string[] { "docuserial", "documovie" };
                            break;
                        case "57":
                            types = new string[] { "tvshow" };
                            break;
                        case "76":
                            types = new string[] { "multfilm", "multserial" };
                            break;
                    }
                    #endregion

                    if (!validrq && !TorrentCache.Exists(TorrentFileMemKey(downloadid)))
                        continue;

                    torrents.Add(new TorrentDetails()
                    {
                        trackerName = "megapeer",
                        types = types,
                        url = url,
                        title = title,
                        sid = 1,
                        sizeName = sizeName,
                        parselink = $"{host}/megapeer/parsemagnet?id={downloadid}" + (!validrq ? "&usecache=true" : ""),
                        createTime = createTime,
                        name = name,
                        originalname = originalname,
                        relased = relased
                    });
                }
            }

            return true;
        }
    }
}

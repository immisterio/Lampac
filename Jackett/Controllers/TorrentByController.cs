using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Lampac.Engine.CORE;
using Lampac.Engine.Parse;
using Lampac.Engine;
using System.Collections.Concurrent;
using Lampac.Models.JAC;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared;

namespace Lampac.Controllers.JAC
{
    [Route("torrentby/[action]")]
    public class TorrentByController : BaseController
    {
        #region parseMagnet
        async public Task<ActionResult> parseMagnet(int id, string magnet)
        {
            if (!AppInit.conf.TorrentBy.enable || AppInit.conf.TorrentBy.priority != "torrent")
                return Content("disable");

            string key = $"torrentby:parseMagnet:{id}";
            if (id == 0 || Startup.memoryCache.TryGetValue($"{key}:error", out _))
                return Redirect(magnet);

            if (Startup.memoryCache.TryGetValue(key, out byte[] _t))
                return File(_t, "application/x-bittorrent");

            _t = await HttpClient.Download($"{AppInit.conf.TorrentBy.host}/d.php?id={id}", referer: AppInit.conf.TorrentBy.host, timeoutSeconds: 10);
            if (_t != null && BencodeTo.Magnet(_t) != null)
            {
                await TorrentCache.Write(key, _t);
                Startup.memoryCache.Set(key, _t, DateTime.Now.AddMinutes(Math.Max(1, AppInit.conf.jac.torrentCacheToMinutes)));
                return File(_t, "application/x-bittorrent");
            }
            else if (AppInit.conf.jac.emptycache)
                Startup.memoryCache.Set($"{key}:error", 0, DateTime.Now.AddMinutes(Math.Max(1, AppInit.conf.jac.torrentCacheToMinutes)));

            return Redirect(magnet);
        }
        #endregion

        async public static Task<bool> parsePage(string host, ConcurrentBag<TorrentDetails> torrents, string query, string cat)
        {
            if (!AppInit.conf.TorrentBy.enable)
                return false;

            #region Кеш html
            string cachekey = $"torrentby:{cat}:{query}";
            var cread = await HtmlCache.Read(cachekey);
            string priority = cread.cache ? AppInit.conf.TorrentBy.priority : "magnet";

            if (cread.emptycache)
                return false;

            if (!cread.cache)
            {
                string html = await HttpClient.Get($"{AppInit.conf.TorrentBy.host}/search/?search={HttpUtility.UrlEncode(query)}&category={cat}&search_in=0", useproxy: AppInit.conf.TorrentBy.useproxy, timeoutSeconds: AppInit.conf.jac.timeoutSeconds);

                if (html != null)
                {
                    cread.html = html;
                    await HtmlCache.Write(cachekey, html);
                    priority = AppInit.conf.TorrentBy.priority;
                }

                if (cread.html == null)
                {
                    HtmlCache.EmptyCache(cachekey);
                    return false;
                }
            }
            #endregion

            foreach (string row in cread.html.Split("<tr class=\"ttable_col").Skip(1))
            {
                #region Локальный метод - Match
                string Match(string pattern, int index = 1)
                {
                    string res = HttpUtility.HtmlDecode(new Regex(pattern, RegexOptions.IgnoreCase).Match(row).Groups[index].Value.Trim());
                    res = Regex.Replace(res, "[\n\r\t ]+", " ");
                    return res.Trim();
                }
                #endregion

                if (string.IsNullOrWhiteSpace(row) || !row.Contains("magnet:?xt=urn"))
                    continue;

                #region Дата создания
                DateTime createTime = default;

                if (row.Contains(">Сегодня</td>"))
                {
                    createTime = DateTime.Today;
                }
                else if (row.Contains(">Вчера</td>"))
                {
                    createTime = DateTime.Today.AddDays(-1);
                }
                else
                {
                    string _createTime = Match(">([0-9]{4}-[0-9]{2}-[0-9]{2})</td>").Replace("-", " ");
                    if (!DateTime.TryParseExact(_createTime, "yyyy MM dd", new CultureInfo("ru-RU"), DateTimeStyles.None, out createTime))
                        continue;
                }

                //if (createTime == default)
                //    continue;
                #endregion

                #region Данные раздачи
                string url = Match("<a name=\"search_select\" [^>]+ href=\"/([0-9]+/[^\"]+)\"");
                string title = Match("<a name=\"search_select\" [^>]+>([^<]+)</a>");
                string _sid = Match("<font color=\"green\">&uarr; ([0-9]+)</font>");
                string _pir = Match("<font color=\"red\">&darr; ([0-9]+)</font>");
                string sizeName = Match("</td><td style=\"white-space:nowrap;\">([^<]+)</td>");
                string magnet = Match("href=\"(magnet:\\?xt=[^\"]+)\"");
                string viewtopic = Regex.Match(url, "^([0-9]+)").Groups[1].Value;

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(magnet))
                    continue;

                url = $"{AppInit.conf.TorrentBy.host}/{url}";
                #endregion

                #region Парсим раздачи
                int relased = 0;
                string name = null, originalname = null;

                if (cat == "1")
                {
                    #region Зарубежные фильмы
                    // Код бессмертия / Код молодости / Eternal Code (2019)
                    var g = Regex.Match(title, "^([^/\\(]+) / [^/]+ / ([^/\\(]+) \\(([0-9]{4})\\)").Groups;
                    if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                    {
                        name = g[1].Value;
                        originalname = g[2].Value;

                        if (int.TryParse(g[3].Value, out int _yer))
                            relased = _yer;
                    }
                    else
                    {
                        // Брешь / Breach (2020)
                        g = Regex.Match(title, "^([^/\\(]+) / ([^/\\(]+) \\(([0-9]{4})\\)").Groups;

                        name = g[1].Value;
                        originalname = g[2].Value;

                        if (int.TryParse(g[3].Value, out int _yer))
                            relased = _yer;
                    }
                    #endregion
                }
                else if (cat == "2")
                {
                    #region Наши фильмы
                    // Временная связь (2020)
                    // Приключения принца Флоризеля / Клуб самоубийц или Приключения титулованной особы (1979)
                    var g = Regex.Match(title, "^([^/\\(]+) (/ [^/\\(]+)?\\(([0-9]{4})\\)").Groups;
                    name = g[1].Value;

                    if (int.TryParse(g[3].Value, out int _yer))
                        relased = _yer;
                    #endregion
                }
                else if (cat == "3")
                {
                    #region Сериалы
                    // Голяк / Без гроша / Без денег / Brassic [S04] (2022) WEB-DLRip | Ozz
                    var g = Regex.Match(title, "^([^/\\(\\[]+) / [^/]+ / [^/]+ / ([^/\\(\\[]+) \\[[^\\]]+\\] +\\(([0-9]{4})(\\)|-)").Groups;
                    if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                    {
                        name = g[1].Value;
                        originalname = g[2].Value;

                        if (int.TryParse(g[3].Value, out int _yer))
                            relased = _yer;
                    }
                    else
                    {
                        // Перевал / Der Pass / Pagan Peak [S01] (2018)
                        g = Regex.Match(title, "^([^/\\(\\[]+) / [^/]+ / ([^/\\(\\[]+) \\[[^\\]]+\\] +\\(([0-9]{4})(\\)|-)").Groups;
                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                        {
                            name = g[1].Value;
                            originalname = g[2].Value;

                            if (int.TryParse(g[3].Value, out int _yer))
                                relased = _yer;
                        }
                        else
                        {
                            // Стража / The Watch [01x01-05 из 08] (2020)
                            g = Regex.Match(title, "^([^/\\(\\[]+) / ([^/\\[]+) \\[[^\\]]+\\] +\\(([0-9]{4})(\\)|-)").Groups;
                            if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                            {
                                name = g[1].Value;
                                originalname = g[2].Value;

                                if (int.TryParse(g[3].Value, out int _yer))
                                    relased = _yer;
                            }
                            else
                            {
                                // Стажёры [01-10 из 24] (2019)
                                g = Regex.Match(title, "^([^/\\(\\[]+) \\[[^\\]]+\\] +\\(([0-9]{4})(\\)|-)").Groups;

                                name = g[1].Value;
                                if (int.TryParse(g[2].Value, out int _yer))
                                    relased = _yer;
                            }
                        }
                    }
                    #endregion
                }
                else if (cat == "5" || cat == "6" || cat == "4" || cat == "12")
                {
                    #region Мультфильмы / Аниме / Телевизор / Юмор
                    if (title.Contains(" / "))
                    {
                        if (title.Contains("[") && title.Contains("]"))
                        {
                            // 	Разочарование / еще название / Disenchantment [S03] (2021)
                            var g = Regex.Match(title, "^([^/]+) / [^/]+ / ([^/\\[]+) \\[[^\\]]+\\] +\\(([0-9]{4})(\\)|-)").Groups;
                            if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                            {
                                name = g[1].Value;
                                originalname = g[2].Value;

                                if (int.TryParse(g[3].Value, out int _yer))
                                    relased = _yer;
                            }
                            else
                            {
                                // 	Разочарование / Disenchantment [S03] (2021)
                                g = Regex.Match(title, "^([^/]+) / ([^/\\[]+) \\[[^\\]]+\\] +\\(([0-9]{4})(\\)|-)").Groups;

                                name = g[1].Value;
                                originalname = g[2].Value;

                                if (int.TryParse(g[3].Value, out int _yer))
                                    relased = _yer;
                            }
                        }
                        else
                        {
                            // 	Душа / еще название / Soul (2020)
                            var g = Regex.Match(title, "^([^/\\(]+) / [^/]+ / ([^/\\(]+) \\(([0-9]{4})(\\)|-)").Groups;
                            if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                            {
                                name = g[1].Value;
                                originalname = g[2].Value;

                                if (int.TryParse(g[3].Value, out int _yer))
                                    relased = _yer;
                            }
                            else
                            {
                                // Душа / Soul (2020)
                                // Галактики / Galaxies (2017-2019)
                                g = Regex.Match(title, "^([^/\\(]+) / ([^/\\(]+) \\(([0-9]{4})(\\)|-)").Groups;

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
                            // 	Непокоренные [01-04 из 04] (2020)
                            var g = Regex.Match(title, "^([^/\\[]+) \\[[^\\]]+\\] +\\(([0-9]{4})(\\)|-)").Groups;
                            name = g[1].Value;

                            if (int.TryParse(g[2].Value, out int _yer))
                                relased = _yer;
                        }
                        else
                        {
                            // Душа (2020)
                            var g = Regex.Match(title, "^([^/\\(]+) \\(([0-9]{4})(\\)|-)").Groups;
                            name = g[1].Value;

                            if (int.TryParse(g[2].Value, out int _yer))
                                relased = _yer;
                        }
                    }
                    #endregion
                }
                #endregion

                if (string.IsNullOrWhiteSpace(name))
                    name = Regex.Split(title, "(\\[|\\/|\\(|\\|)", RegexOptions.IgnoreCase)[0].Trim();

                if (!string.IsNullOrWhiteSpace(name) || cat == "0")
                {
                    #region types
                    string[] types = new string[] { };
                    switch (cat)
                    {
                        case "1":
                        case "2":
                            types = new string[] { "movie" };
                            break;
                        case "3":
                            types = new string[] { "serial" };
                            break;
                        case "4":
                        case "12":
                            types = new string[] { "tvshow" };
                            break;
                        case "5":
                            types = new string[] { "multfilm", "multserial" };
                            break;
                        case "6":
                            types = new string[] { "anime" };
                            break;
                    }
                    #endregion

                    int.TryParse(_sid, out int sid);
                    int.TryParse(_pir, out int pir);

                    torrents.Add(new TorrentDetails()
                    {
                        trackerName = "torrentby",
                        types = types,
                        url = url,
                        title = title,
                        sid = sid,
                        pir = pir,
                        sizeName = sizeName,
                        magnet = priority == "torrent" ? null : magnet,
                        parselink = priority == "torrent" ? $"{host}/torrentby/parsemagnet?id={viewtopic}&magnet={HttpUtility.UrlEncode(magnet)}" : null,
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

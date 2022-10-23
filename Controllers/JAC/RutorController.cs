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

namespace Lampac.Controllers.JAC
{
    //[Route("rutor/[action]")]
    public class RutorController : BaseController
    {
        async public static Task<bool> parsePage(ConcurrentBag<TorrentDetails> torrents, string query, string cat, bool isua = false, string parsecat = null)
        {
            if (!AppInit.conf.Rutor.enable)
                return false;

            #region Кеш
            string cachekey = $"rutor:{cat}:{query}:{isua}";
            if (!HtmlCache.Read(cachekey, out string cachehtml))
            {
                string html = await HttpClient.Get($"{AppInit.conf.Rutor.host}/search" + (cat == "0" ? $"/{HttpUtility.UrlEncode(query)}" : $"/0/{cat}/000/0/{HttpUtility.UrlEncode(query)}"), useproxy: AppInit.conf.Rutor.useproxy, timeoutSeconds: AppInit.conf.timeoutSeconds);

                if (html != null)
                {
                    cachehtml = html;
                    HtmlCache.Write(cachekey, html);
                }

                if (cachehtml == null)
                    return false;
            }
            #endregion

            foreach (string row in Regex.Split(Regex.Replace(cachehtml.Split("</span></td></tr></table><b>")[0], "[\n\r\t]+", ""), "<tr class=\"(gai|tum)\">").Skip(1))
            {
                #region Локальный метод - Match
                string Match(string pattern, int index = 1)
                {
                    string res = HttpUtility.HtmlDecode(new Regex(pattern, RegexOptions.IgnoreCase).Match(row).Groups[index].Value.Trim());
                    res = Regex.Replace(res, "[\n\r\t ]+", " ");
                    return res.Replace(" ", " ").Trim(); // Меняем непонятный символ похожий на проблел, на обычный проблел
                }
                #endregion

                if (string.IsNullOrWhiteSpace(row) || !row.Contains("magnet:?xt=urn"))
                    continue;

                #region createTime
                DateTime createTime = tParse.ParseCreateTime(Match("<td>([^<]+)</td><td([^>]+)?><a class=\"downgif\""), "dd.MM.yy");
                //if (createTime == default)
                //    continue;
                #endregion

                #region Данные раздачи
                string url = Match("<a href=\"/(torrent/[^\"]+)\">");
                string title = Match("<a href=\"/torrent/[^\"]+\">([^<]+)</a>");
                string _sid = Match("<span class=\"green\"><img [^>]+>&nbsp;([0-9]+)</span>");
                string _pir = Match("<span class=\"red\">&nbsp;([0-9]+)</span>");
                string sizeName = Match("<td align=\"right\">([^<]+)</td>");
                string magnet = Match("href=\"(magnet:\\?xt=[^\"]+)\"");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(magnet) || title.ToLower().Contains("трейлер"))
                    continue;

                if (isua && !title.Contains(" UKR"))
                    continue;

                url = $"{AppInit.conf.Rutor.host}/{url}";
                #endregion

                #region Парсим раздачи
                int relased = 0;
                string name = null, originalname = null;

                if (cat == "1" || parsecat == "1")
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
                else if (cat == "5")
                {
                    #region Наши фильмы
                    var g = Regex.Match(title, "^([^/\\(]+) \\(([0-9]{4})\\)").Groups;
                    name = g[1].Value;

                    if (int.TryParse(g[2].Value, out int _yer))
                        relased = _yer;
                    #endregion
                }
                else if (cat == "4" || parsecat == "4")
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
                else if (cat == "16")
                {
                    #region Наши сериалы
                    var g = Regex.Match(title, "^([^/]+) \\[[^\\]]+\\] \\(([0-9]{4})(\\)|-)").Groups;
                    name = g[1].Value;

                    if (int.TryParse(g[2].Value, out int _yer))
                        relased = _yer;
                    #endregion
                }
                else if (cat == "12" || cat == "6" || cat == "7" || parsecat == "7" || cat == "10" || parsecat == "10")
                {
                    #region Научно-популярные фильмы / Телевизор / Мультипликация / Аниме
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
                    switch (parsecat ?? cat)
                    {
                        case "1":
                        case "5":
                            types = new string[] { "movie" };
                            break;
                        case "4":
                        case "16":
                            types = new string[] { "serial" };
                            break;
                        case "12":
                            types = new string[] { "docuserial", "documovie" };
                            break;
                        case "6":
                            types = new string[] { "tvshow" };
                            break;
                        case "7":
                            types = new string[] { "multfilm", "multserial" };
                            break;
                        case "10":
                            types = new string[] { "anime" };
                            break;
                    }
                    #endregion

                    int.TryParse(_sid, out int sid);
                    int.TryParse(_pir, out int pir);

                    torrents.Add(new TorrentDetails()
                    {
                        trackerName = "rutor",
                        types = types,
                        url = url,
                        title = title,
                        sid = sid,
                        pir = pir,
                        sizeName = sizeName,
                        magnet = magnet,
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

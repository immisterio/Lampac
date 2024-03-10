using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Lampac.Engine.CORE;
using Lampac.Engine.Parse;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared;
using Shared.Engine.CORE;
using JacRed.Engine;
using JacRed.Models;
using System.Collections.Generic;

namespace Lampac.Controllers.JAC
{
    [Route("rutor/[action]")]
    public class RutorController : JacBaseController
    {
        #region parseMagnet
        async public Task<ActionResult> parseMagnet(int id, string magnet)
        {
            if (!jackett.Rutor.enable || jackett.Rutor.priority != "torrent")
                return Content("disable");

            string key = $"rutor:parseMagnet:{id}";
            if (id == 0 || Startup.memoryCache.TryGetValue($"{key}:error", out _))
                return Redirect(magnet);

            if (Startup.memoryCache.TryGetValue(key, out byte[] _t))
                return File(_t, "application/x-bittorrent");

            var proxyManager = new ProxyManager("rutor", jackett.Rutor);

            _t = await HttpClient.Download($"{Regex.Replace(jackett.Rutor.host, "^(https?:)//", "$1//d.")}/download/{id}", referer: jackett.Rutor.host, timeoutSeconds: 10, proxy: proxyManager.Get());
            if (_t != null && BencodeTo.Magnet(_t) != null)
            {
                if (jackett.cache)
                {
                    TorrentCache.Write(key, _t);
                    Startup.memoryCache.Set(key, _t, DateTime.Now.AddMinutes(Math.Max(1, jackett.torrentCacheToMinutes)));
                }

                return File(_t, "application/x-bittorrent");
            }
            else if (jackett.emptycache && jackett.cache)
                Startup.memoryCache.Set($"{key}:error", 0, DateTime.Now.AddMinutes(Math.Max(1, jackett.torrentCacheToMinutes)));

            proxyManager.Refresh();
            return Redirect(magnet);
        }
        #endregion


        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query, string cat, bool isua = false, string parsecat = null)
        {
            if (!jackett.Rutor.enable)
                return Task.FromResult(false);

            return JackettCache.Invoke($"rutor:{cat}:{query}:{isua}", torrents, () => parsePage(host, query, cat, isua, parsecat));
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query, string cat, bool isua, string parsecat)
        {
            // fix search
            query = query.Replace("\"", " ").Replace("'", " ").Replace("?", " ").Replace("&", " ");

            var proxyManager = new ProxyManager("rutor", jackett.Rutor);

            string html = await HttpClient.Get($"{jackett.Rutor.host}/search" + (cat == "0" ? $"/{HttpUtility.UrlEncode(query)}" : $"/0/{cat}/000/0/{HttpUtility.UrlEncode(query)}"), proxy: proxyManager.Get(), timeoutSeconds: jackett.timeoutSeconds);

            if (html == null || !html.Contains("id=\"logo\""))
            {
                proxyManager.Refresh();
                return null;
            }

            var torrents = new List<TorrentDetails>();

            foreach (string row in Regex.Split(Regex.Replace(html.Split("</span></td></tr></table><b>")[0], "[\n\r\t]+", ""), "<tr class=\"(gai|tum)\">").Skip(1))
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
                string viewtopic = Regex.Match(url, "torrent/([0-9]+)").Groups[1].Value;

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(magnet) || title.ToLower().Contains("трейлер"))
                    continue;

                if (isua && !title.Contains(" UKR"))
                    continue;

                url = $"{jackett.Rutor.host}/{url}";
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
                else if (cat == "12" || cat == "6" || cat == "7" || parsecat == "7" || cat == "10" || parsecat == "10" || cat == "15" || cat == "13")
                {
                    #region Научно-популярные фильмы / Телевизор / Мультипликация / Аниме / Юмор / Спорт и Здоровье
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

                if (string.IsNullOrWhiteSpace(name))
                    name = Regex.Split(title, "(\\[|\\/|\\(|\\|)", RegexOptions.IgnoreCase)[0].Trim();

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
                        case "15":
                            types = new string[] { "tvshow" };
                            break;
                        case "7":
                            types = new string[] { "multfilm", "multserial" };
                            break;
                        case "10":
                            types = new string[] { "anime" };
                            break;
                        case "13":
                            types = new string[] { "sport" };
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
                        magnet = jackett.Rutor.priority == "torrent" ? null : magnet,
                        parselink = jackett.Rutor.priority == "torrent" ? $"{host}/rutor/parsemagnet?id={viewtopic}&magnet={HttpUtility.UrlEncode(magnet)}" : null,
                        createTime = createTime,
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

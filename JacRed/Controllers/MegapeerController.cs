using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Engine.Parse;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Shared;
using System.Collections.Generic;
using Shared.Engine.CORE;
using JacRed.Engine;
using JacRed.Models;
using Shared.Model.Online;

namespace Lampac.Controllers.JAC
{
    [Route("megapeer/[action]")]
    public class MegapeerController : JacBaseController
    {
        #region parseMagnet
        async public Task<ActionResult> parseMagnet(string id)
        {
            if (!jackett.Megapeer.enable)
                return Content("disable");

            string key = $"megapeer:parseMagnet:{id}";
            if (Startup.memoryCache.TryGetValue(key, out byte[] _m))
                return File(_m, "application/x-bittorrent");

            if (Startup.memoryCache.TryGetValue($"{key}:error", out _))
            {
                if (TorrentCache.Read(key) is var tc && tc.cache)
                    return File(tc.torrent, "application/x-bittorrent");

                return Content("error");
            }

            var proxyManager = new ProxyManager("megapeer", jackett.Megapeer);

            byte[] _t = await HttpClient.Download($"{jackett.Megapeer.host}/download/{id}", referer: jackett.Megapeer.host, timeoutSeconds: 10, proxy: proxyManager.Get());
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
                Startup.memoryCache.Set($"{key}:error", 0, DateTime.Now.AddMinutes(1));

            if (TorrentCache.Read(key) is var tcache && tcache.cache)
                return File(tcache.torrent, "application/x-bittorrent");

            proxyManager.Refresh();
            return Content("error");
        }
        #endregion


        #region search
        public static Task<bool> search(string host, ConcurrentBag<TorrentDetails> torrents, string query, string cat)
        {
            if (!jackett.Megapeer.enable)
                return Task.FromResult(false);

            return JackettCache.Invoke($"megapeer:{cat}:{query}", torrents, () => parsePage(host, query, cat));
        }
        #endregion

        #region parsePage
        async static ValueTask<List<TorrentDetails>> parsePage(string host, string query, string cat)
        {
            var torrents = new List<TorrentDetails>();

            #region html
            var proxyManager = new ProxyManager("megapeer", jackett.Megapeer);

            string html = await HttpClient.Get($"{jackett.Megapeer.host}/browse.php?search={HttpUtility.UrlEncode(query, Encoding.GetEncoding(1251))}", encoding: Encoding.GetEncoding(1251), proxy: proxyManager.Get(), timeoutSeconds: jackett.timeoutSeconds, headers: HeadersModel.Init(
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("referer", $"{jackett.Megapeer.host}"),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-mode", "navigate"),
                ("sec-fetch-site", "same-origin"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1")
            ));

            if (html == null || !html.Contains("id=\"logo\""))
            {
                proxyManager.Refresh();
                return null;
            }
            #endregion

            foreach (string row in html.Split("class=\"tCenter hl-tr\"").Skip(1))
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

                url = $"{jackett.Megapeer.host}/{url}";
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

                if (string.IsNullOrWhiteSpace(name))
                    name = Regex.Split(title, "(\\[|\\/|\\(|\\|)", RegexOptions.IgnoreCase)[0].Trim();

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

                    torrents.Add(new TorrentDetails()
                    {
                        trackerName = "megapeer",
                        types = types,
                        url = url,
                        title = title,
                        sid = 1,
                        sizeName = sizeName,
                        parselink = $"{host}/megapeer/parsemagnet?id={downloadid}",
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

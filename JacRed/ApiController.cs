using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Lampac.Engine;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Caching.Memory;
using System.Threading.Tasks;
using System;
using System.Web;
using MonoTorrent;
using JacRed.Models.Details;
using JacRed.Engine;
using Lampac;
using Lampac.Engine.CORE;
using Jackett;

namespace JacRed.Controllers
{
    public class ApiController : BaseController
    {
        [Route("api/v1.0/conf")]
        public JsonResult JacRedConf(string apikey)
        {
            return Json(new
            {
                apikey = string.IsNullOrWhiteSpace(AppInit.conf.jac.apikey) || apikey == AppInit.conf.jac.apikey
            });
        }

        #region Jackett
        [Route("/api/v2.0/indexers/{status}/results")]
        public ActionResult Jackett(string query, string title, string title_original, int year, int is_serial, Dictionary<string, string> category)
        {
            bool rqnum = false, setcache = false;
            var torrents = new Dictionary<string, TorrentDetails>();

            #region Запрос с NUM
            var mNum = Regex.Match(query ?? string.Empty, "^([^a-z-A-Z]+) ([^а-я-А-Я]+) ([0-9]{4})$");

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(title_original) &&
                mNum.Success)
            {
                if (Regex.IsMatch(mNum.Groups[2].Value, "[a-zA-Z]{4}"))
                {
                    rqnum = true;
                    var g = mNum.Groups;

                    title = g[1].Value;
                    title_original = g[2].Value;
                    year = int.Parse(g[3].Value);
                }
            }
            #endregion

            #region category
            if (is_serial == 0 && category != null)
            {
                string cat = category.FirstOrDefault().Value;
                if (cat != null)
                {
                    if (cat.Contains("5020") || cat.Contains("2010"))
                        is_serial = 3; // tvshow
                    else if (cat.Contains("5080"))
                        is_serial = 4; // док
                    else if (cat.Contains("5070"))
                        is_serial = 5; // аниме
                    else if (is_serial == 0)
                    {
                        if (cat.StartsWith("20"))
                            is_serial = 1; // фильм
                        else if (cat.StartsWith("50"))
                            is_serial = 2; // сериал
                    }
                }
            }
            #endregion

            #region AddTorrents
            void AddTorrents(TorrentDetails t)
            {
                if (torrents.TryGetValue(t.url, out TorrentDetails val))
                {
                    if (t.updateTime > val.updateTime)
                        torrents[t.url] = t;
                }
                else
                {
                    torrents.TryAdd(t.url, t);
                }
            }
            #endregion

            string memoryKey = $"{ModInit.conf.mergeduplicates}:{rqnum}:{title}:{title_original}:{year}:{is_serial}";
            if (memoryCache.TryGetValue(memoryKey, out string jval))
                return Content(jval, "application/json; charset=utf-8");

            if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(title_original))
            {
                #region Точный поиск
                setcache = true;

                string _n = StringConvert.SearchName(title);
                string _o = StringConvert.SearchName(title_original);

                // Быстрая выборка по совпадению ключа в имени
                foreach (var val in FileDB.masterDb.Where(i => (_n != null && i.Key.StartsWith($"{_n}:")) || (_o != null && i.Key.EndsWith($":{_o}"))).Take(ModInit.conf.maxreadfile))
                {
                    foreach (var t in FileDB.OpenRead(val.Key).Values)
                    {
                        if (t.types == null)
                            continue;

                        string name = StringConvert.SearchName(t.name);
                        string originalname = StringConvert.SearchName(t.originalname);

                        // Точная выборка по name или originalname
                        if ((_n != null && _n == name) || (_o != null && _o == originalname))
                        {
                            if (is_serial == 1)
                            {
                                #region Фильм
                                if (t.types.Contains("movie") || t.types.Contains("multfilm") || t.types.Contains("anime") || t.types.Contains("documovie"))
                                {
                                    if (year > 0)
                                    {
                                        if (t.relased == year || t.relased == (year - 1) || t.relased == (year + 1))
                                            AddTorrents(t);
                                    }
                                    else
                                    {
                                        AddTorrents(t);
                                    }
                                }
                                #endregion
                            }
                            else if (is_serial == 2)
                            {
                                #region Сериал
                                if (t.types.Contains("serial") || t.types.Contains("multserial") || t.types.Contains("anime") || t.types.Contains("docuserial") || t.types.Contains("tvshow"))
                                {
                                    if (year > 0)
                                    {
                                        if (t.relased >= (year - 1))
                                            AddTorrents(t);
                                    }
                                    else
                                    {
                                        AddTorrents(t);
                                    }
                                }
                                #endregion
                            }
                            else if (is_serial == 3)
                            {
                                #region tvshow
                                if (t.types.Contains("tvshow"))
                                {
                                    if (year > 0)
                                    {
                                        if (t.relased >= (year - 1))
                                            AddTorrents(t);
                                    }
                                    else
                                    {
                                        AddTorrents(t);
                                    }
                                }
                                #endregion
                            }
                            else if (is_serial == 4)
                            {
                                #region docuserial / documovie
                                if (t.types.Contains("docuserial") || t.types.Contains("documovie"))
                                {
                                    if (year > 0)
                                    {
                                        if (t.relased >= (year - 1))
                                            AddTorrents(t);
                                    }
                                    else
                                    {
                                        AddTorrents(t);
                                    }
                                }
                                #endregion
                            }
                            else if (is_serial == 5)
                            {
                                #region anime
                                if (t.types.Contains("anime"))
                                {
                                    if (year > 0)
                                    {
                                        if (t.relased >= (year - 1))
                                            AddTorrents(t);
                                    }
                                    else
                                    {
                                        AddTorrents(t);
                                    }
                                }
                                #endregion
                            }
                            else
                            {
                                #region Неизвестно
                                if (year > 0)
                                {
                                    if (t.types.Contains("movie") || t.types.Contains("multfilm") || t.types.Contains("documovie"))
                                    {
                                        if (t.relased == year || t.relased == (year - 1) || t.relased == (year + 1))
                                            AddTorrents(t);
                                    }
                                    else
                                    {
                                        if (t.relased >= (year - 1))
                                            AddTorrents(t);
                                    }
                                }
                                else
                                {
                                    AddTorrents(t);
                                }
                                #endregion
                            }
                        }
                    }

                }
                #endregion
            }
            else if (!string.IsNullOrWhiteSpace(query) && query.Length > 3)
            {
                #region Обычный поиск
                string _s = StringConvert.SearchName(query);

                #region torrentsSearch
                void torrentsSearch(bool exact)
                {
                    foreach (var val in FileDB.masterDb.OrderByDescending(i => i.Value).Where(i => i.Key.Contains(_s)).Take(ModInit.conf.maxreadfile))
                    {
                        foreach (var t in FileDB.OpenRead(val.Key).Values)
                        {
                            if (exact)
                            {
                                if (StringConvert.SearchName(t.name) != _s && StringConvert.SearchName(t.originalname) != _s)
                                    continue;
                            }

                            if (t.types == null)
                                continue;

                            if (is_serial == 1)
                            {
                                if (t.types.Contains("movie") || t.types.Contains("multfilm") || t.types.Contains("anime") || t.types.Contains("documovie"))
                                    AddTorrents(t);
                            }
                            else if (is_serial == 2)
                            {
                                if (t.types.Contains("serial") || t.types.Contains("multserial") || t.types.Contains("anime") || t.types.Contains("docuserial") || t.types.Contains("tvshow"))
                                    AddTorrents(t);
                            }
                            else if (is_serial == 3)
                            {
                                if (t.types.Contains("tvshow"))
                                    AddTorrents(t);
                            }
                            else if (is_serial == 4)
                            {
                                if (t.types.Contains("docuserial") || t.types.Contains("documovie"))
                                    AddTorrents(t);
                            }
                            else if (is_serial == 5)
                            {
                                if (t.types.Contains("anime"))
                                    AddTorrents(t);
                            }
                            else
                            {
                                AddTorrents(t);
                            }
                        }

                    }
                }
                #endregion

                torrentsSearch(exact: true);
                if (torrents.Count == 0)
                    torrentsSearch(exact: false);
                #endregion
            }

            #region getCategoryIds
            HashSet<int> getCategoryIds(TorrentDetails t, out string categoryDesc)
            {
                categoryDesc = null;
                HashSet<int> categoryIds = new HashSet<int>();

                foreach (string type in t.types)
                {
                    switch (type)
                    {
                        case "movie":
                            categoryDesc = "Movies";
                            categoryIds.Add(2000);
                            break;

                        case "serial":
                            categoryDesc = "TV";
                            categoryIds.Add(5000);
                            break;

                        case "documovie":
                        case "docuserial":
                            categoryDesc = "TV/Documentary";
                            categoryIds.Add(5080);
                            break;

                        case "tvshow":
                            categoryDesc = "TV/Foreign";
                            categoryIds.Add(5020);
                            categoryIds.Add(2010);
                            break;

                        case "anime":
                            categoryDesc = "TV/Anime";
                            categoryIds.Add(5070);
                            break;
                    }
                }

                return categoryIds;
            }
            #endregion

            #region Объединить дубликаты
            var tsort = new List<TorrentDetails>();

            if (!ModInit.conf.mergeduplicates || rqnum)
            {
                tsort = torrents.Values.Where(i => ModInit.conf.trackers == null || ModInit.conf.trackers.Contains(i.trackerName)).ToList();
            }
            else 
            {
                Dictionary<string, (TorrentDetails torrent, string title, string Name, List<string> AnnounceUrls)> temp = new Dictionary<string, (TorrentDetails, string, string, List<string>)>();

                foreach (var torrent in torrents.Values.Where(i => ModInit.conf.trackers == null || ModInit.conf.trackers.Contains(i.trackerName)).ToList())
                {
                    var magnetLink = MagnetLink.Parse(torrent.magnet);
                    string hex = magnetLink.InfoHash.ToHex();

                    if (!temp.TryGetValue(hex, out _))
                    {
                        temp.TryAdd(hex, ((TorrentDetails)torrent.Clone(), torrent.trackerName == "kinozal" ? torrent.title : null, magnetLink.Name, magnetLink.AnnounceUrls?.ToList() ?? new List<string>()));
                    }
                    else
                    {
                        var t = temp[hex];
                        t.torrent.trackerName += $", {torrent.trackerName}";

                        #region UpdateMagnet
                        void UpdateMagnet()
                        {
                            string magnet = $"magnet:?xt=urn:btih:{hex.ToLower()}";

                            if (!string.IsNullOrWhiteSpace(t.Name))
                                magnet += $"&dn={HttpUtility.UrlEncode(t.Name)}";

                            if (t.AnnounceUrls.Count > 0)
                            {
                                foreach (string announce in t.AnnounceUrls)
                                {
                                    string tr = announce.Contains("/") || announce.Contains(":") ? HttpUtility.UrlEncode(announce) : announce;

                                    if (!magnet.Contains(tr))
                                        magnet += $"&tr={tr}";
                                }
                            }

                            t.torrent.magnet= magnet ;
                        }
                        #endregion

                        if (string.IsNullOrWhiteSpace(t.Name) && !string.IsNullOrWhiteSpace(magnetLink.Name))
                        {
                            t.Name = magnetLink.Name;
                            temp[hex] = t;
                            UpdateMagnet();
                        }

                        if (magnetLink.AnnounceUrls != null && magnetLink.AnnounceUrls.Count > 0)
                        {
                            t.AnnounceUrls.AddRange(magnetLink.AnnounceUrls);
                            UpdateMagnet();
                        }

                        #region UpdateTitle
                        void UpdateTitle()
                        {
                            if (string.IsNullOrWhiteSpace(t.title))
                                return;

                            string title = t.title;

                            if (t.torrent.voices != null && t.torrent.voices.Count > 0)
                                title += $" | {string.Join(" | ", t.torrent.voices)}";

                            t.torrent.title = title;
                        }

                        if (torrent.trackerName == "kinozal")
                        {
                            t.title = torrent.title;
                            temp[hex] = t;
                            UpdateTitle();
                        }

                        if (torrent.voices != null && torrent.voices.Count > 0)
                        {
                            if (t.torrent.voices == null)
                            {
                                t.torrent.voices = torrent.voices;
                            }
                            else
                            {
                                foreach (var v in torrent.voices)
                                    t.torrent.voices.Add(v);
                            }

                            UpdateTitle();
                        }
                        #endregion

                        if (torrent.sid > t.torrent.sid)
                            t.torrent.sid = torrent.sid;

                        if (torrent.pir > t.torrent.pir)
                            t.torrent.pir = torrent.pir;

                        if (torrent.createTime > t.torrent.createTime)
                            t.torrent.createTime = torrent.createTime;
                    }
                }

                foreach (var item in temp.Select(i => i.Value.torrent))
                    tsort.Add(item);
            }
            #endregion

            jval = JsonConvert.SerializeObject(new
            {
                Results = tsort.OrderByDescending(i => i.createTime).Take(2_000).Select(i => new
                {
                    Tracker = i.trackerName,
                    Details = i.url != null && i.url.StartsWith("http") ? i.url : null,
                    Title = i.title,
                    Size = i.size,
                    PublishDate = i.createTime,
                    Category = getCategoryIds(i, out string categoryDesc),
                    CategoryDesc = categoryDesc,
                    Seeders = i.sid,
                    Peers = i.pir,
                    MagnetUri = i.magnet
                })
            });

            if (setcache && !ModInit.conf.evercache)
                memoryCache.Set(memoryKey, jval, DateTime.Now.AddMinutes(10));

            return Content(jval, "application/json; charset=utf-8");
        }
        #endregion

        #region Torrents
        [Route("/api/v1.0/torrents")]
        async public Task<JsonResult> Torrents(string search, string altname, bool exact, string type, string sort, string tracker, string voice, string videotype, long relased, long quality, long season)
        {
            #region search kp/imdb
            if (!string.IsNullOrWhiteSpace(search) && Regex.IsMatch(search.Trim(), "^(tt|kp)[0-9]+$"))
            {
                string memkey = $"api/v1.0/torrents:{search}";
                if (!memoryCache.TryGetValue(memkey, out (string original_name, string name) cache))
                {
                    search = search.Trim();
                    string uri = $"&imdb={search}";
                    if (search.StartsWith("kp"))
                        uri = $"&kp={search.Remove(0, 2)}";

                    var root = await HttpClient.Get<JObject>("https://api.alloha.tv/?token=04941a9a3ca3ac16e2b4327347bbc1" + uri, timeoutSeconds: 8);
                    cache.original_name = root?.Value<JObject>("data")?.Value<string>("original_name");
                    cache.name = root?.Value<JObject>("data")?.Value<string>("name");

                    memoryCache.Set(memkey, cache, DateTime.Now.AddDays(1));
                }

                if (!string.IsNullOrWhiteSpace(cache.name) && !string.IsNullOrWhiteSpace(cache.original_name))
                {
                    search = cache.original_name;
                    altname = cache.name;
                }
                else
                {
                    search = cache.original_name ?? cache.name;
                }
            }
            #endregion

            #region Выборка 
            var torrents = new Dictionary<string, TorrentDetails>();

            #region AddTorrents
            void AddTorrents(TorrentDetails t)
            {
                if (torrents.TryGetValue(t.url, out TorrentDetails val))
                {
                    if (t.updateTime > val.updateTime)
                        torrents[t.url] = t;
                }
                else
                {
                    torrents.TryAdd(t.url, t);
                }
            }
            #endregion

            if (string.IsNullOrWhiteSpace(search) || 3 >= search.Length)
                return Json(torrents);

            string _s = StringConvert.SearchName(search);
            string _altsearch = StringConvert.SearchName(altname);

            if (exact)
            {
                #region Точный поиск
                foreach (var mdb in FileDB.masterDb.Where(i => i.Key.StartsWith($"{_s}:") || i.Key.EndsWith($":{_s}") || (_altsearch != null && i.Key.Contains(_altsearch))))
                {
                    foreach (var t in FileDB.OpenRead(mdb.Key).Values)
                    {
                        if (t.types == null)
                            continue;

                        if (string.IsNullOrWhiteSpace(type) || t.types.Contains(type))
                        {
                            string _n = StringConvert.SearchName(t.name);
                            string _o = StringConvert.SearchName(t.originalname);

                            if (_n == _s || _o == _s || (_altsearch != null && (_n == _altsearch || _o == _altsearch)))
                                AddTorrents(t);
                        }
                    }

                }
                #endregion
            }
            else
            {
                #region Поиск по совпадению ключа в имени
                foreach (var mdb in FileDB.masterDb.OrderByDescending(i => i.Value).Where(i => i.Key.Contains(_s) || (_altsearch != null && i.Key.Contains(_altsearch))).Take(ModInit.conf.maxreadfile))
                {
                    foreach (var t in FileDB.OpenRead(mdb.Key).Values)
                    {
                        if (t.types == null)
                            continue;

                        if (string.IsNullOrWhiteSpace(type) || t.types.Contains(type))
                            AddTorrents(t);
                    }

                }
                #endregion
            }

            if (torrents.Count == 0)
                return Json(torrents);

            IEnumerable<TorrentDetails> query = torrents.Values;

            #region sort
            switch (sort ?? string.Empty)
            {
                case "sid":
                    query = query.OrderByDescending(i => i.sid);
                    break;
                case "pir":
                    query = query.OrderByDescending(i => i.pir);
                    break;
                case "size":
                    query = query.OrderByDescending(i => i.size);
                    break;
                default:
                    query = query.OrderByDescending(i => i.createTime);
                    break;
            }
            #endregion

            if (!string.IsNullOrWhiteSpace(tracker))
                query = query.Where(i => i.trackerName == tracker);

            if (relased > 0)
                query = query.Where(i => i.relased == relased);

            if (quality > 0)
                query = query.Where(i => i.quality == quality);

            if (!string.IsNullOrWhiteSpace(videotype))
                query = query.Where(i => i.videotype == videotype);

            if (!string.IsNullOrWhiteSpace(voice))
                query = query.Where(i => i.voices.Contains(voice));

            if (season > 0)
                query = query.Where(i => i.seasons.Contains((int)season));
            #endregion

            query = query.Where(i => ModInit.conf.trackers == null || ModInit.conf.trackers.Contains(i.trackerName));

            return Json(query.Take(2_000).Select(i => new
            {
                tracker = i.trackerName,
                url = i.url != null && i.url.StartsWith("http") ? i.url : null,
                i.title,
                i.size,
                i.sizeName,
                i.createTime,
                i.sid,
                i.pir,
                i.magnet,
                i.name,
                i.originalname,
                i.relased,
                i.videotype,
                i.quality,
                i.voices,
                i.seasons,
                i.types
            }));
        }
        #endregion
    }
}

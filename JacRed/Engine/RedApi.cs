using MonoTorrent;
using JacRed.Models.AppConf;
using Jackett;

namespace JacRed.Engine
{
    public static class RedApi
    {
        static RedConf red => ModInit.conf.Red;

        #region Indexers
        public static (IEnumerable<TorrentDetails> torrents, bool setcache) Indexers(bool rqnum, string apikey, string query, string title, string title_original, int year, int is_serial, Dictionary<string, string> category)
        {
            bool setcache = false;
            var torrents = new Dictionary<string, TorrentDetails>();

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
                if (t.url == null)
                    return;

                if (!string.IsNullOrEmpty(ModInit.conf.filter) && !Regex.IsMatch(t.title, ModInit.conf.filter, RegexOptions.IgnoreCase))
                    return;

                if (!string.IsNullOrEmpty(ModInit.conf.filter_ignore) && Regex.IsMatch(t.title, ModInit.conf.filter_ignore, RegexOptions.IgnoreCase))
                    return;

                if (InvkEvent.conf.RedApi?.AddTorrents != null)
                {
                    if (!InvkEvent.RedApi("addtorrent", t))
                        return;
                }
                else
                {
                    EventListener.RedApiAddTorrents?.Invoke(t);
                }

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

            if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(title_original))
            {
                #region Точный поиск
                setcache = true;

                string _n = StringConvert.SearchName(title);
                string _o = StringConvert.SearchName(title_original);

                // Быстрая выборка по совпадению ключа в имени
                var mdb = FileDB.masterDb.Where(i => _n != null && i.Key.StartsWith($"{_n}:") || _o != null && i.Key.EndsWith($":{_o}"));
                if (!red.evercache.enable || red.evercache.validHour > 0)
                    mdb = mdb.Take(red.maxreadfile);

                foreach (var val in mdb)
                {
                    using (var fdb = FileDB.Open(val.Key))
                    {
                        foreach (var t in fdb.Database.Values)
                        {
                            if (t.types == null || t.title.Contains(" КПК"))
                                continue;

                            string name = StringConvert.SearchName(t.name);
                            string originalname = StringConvert.SearchName(t.originalname);

                            // Точная выборка по name или originalname
                            if (_n != null && _n == name || _o != null && _o == originalname)
                            {
                                if (is_serial == 1)
                                {
                                    #region Фильм
                                    if (t.types.Contains("movie") || t.types.Contains("multfilm") || t.types.Contains("anime") || t.types.Contains("documovie"))
                                    {
                                        if (Regex.IsMatch(t.title, " (сезон|сери(и|я|й))", RegexOptions.IgnoreCase))
                                            continue;

                                        if (year > 0)
                                        {
                                            if (t.relased == year || t.relased == year - 1 || t.relased == year + 1)
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
                                            if (t.relased >= year - 1)
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
                                            if (t.relased >= year - 1)
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
                                            if (t.relased >= year - 1)
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
                                            if (t.relased >= year - 1)
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
                                            if (t.relased == year || t.relased == year - 1 || t.relased == year + 1)
                                                AddTorrents(t);
                                        }
                                        else
                                        {
                                            if (t.relased >= year - 1)
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
                }
                #endregion
            }
            else if (!string.IsNullOrWhiteSpace(query) && query.Length > 1)
            {
                #region Обычный поиск
                string _s = StringConvert.SearchName(query);

                #region torrentsSearch
                void torrentsSearch(bool exact)
                {
                    var mdb = FileDB.masterDb.Where(i => i.Key.Contains(_s));
                    if (!red.evercache.enable || red.evercache.validHour > 0)
                        mdb = mdb.Take(red.maxreadfile);

                    foreach (var val in mdb)
                    {
                        using (var fdb = FileDB.Open(val.Key))
                        {
                            foreach (var t in fdb.Database.Values)
                            {
                                if (exact)
                                {
                                    if (StringConvert.SearchName(t.name) != _s && StringConvert.SearchName(t.originalname) != _s)
                                        continue;
                                }

                                if (t.types == null || t.title.Contains(" КПК"))
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
                }
                #endregion

                if (is_serial == -1)
                    torrentsSearch(exact: false);
                else
                {
                    torrentsSearch(exact: true);
                    if (torrents.Count == 0)
                        torrentsSearch(exact: false);
                }
                #endregion
            }

            #region Объединить дубликаты
            IEnumerable<TorrentDetails> tsort = null;

            if (ModInit.conf.typesearch == "red" && ((!rqnum && red.mergeduplicates) || (rqnum && red.mergenumduplicates)))
            {
                var temp = new Dictionary<string, (TorrentDetails torrent, string title, string Name, List<string> AnnounceUrls)>();

                foreach (var torrent in torrents.Values
                                                .Where(i => red.trackers == null || red.trackers.Contains(i.trackerName))
                                                .OrderByDescending(i => i.createTime)
                                                .ThenBy(i => i.trackerName == "selezen").ToList())
                {
                    if (torrent.magnet == null)
                        continue;

                    var magnetLink = MagnetLink.Parse(torrent.magnet);
                    string hex = magnetLink.InfoHashes.V1.ToHex();

                    if (!temp.TryGetValue(hex, out _))
                    {
                        temp.TryAdd(hex, ((TorrentDetails)torrent.Clone(), torrent.trackerName == "kinozal" ? torrent.title : null, magnetLink.Name, magnetLink.AnnounceUrls?.ToList() ?? new List<string>()));
                    }
                    else
                    {
                        var t = temp[hex];
                        t.torrent.trackerName += $", {torrent.trackerName}";

                        #region urls
                        if (t.torrent.urls == null)
                            t.torrent.urls = new HashSet<string> { t.torrent.url };

                        t.torrent.urls.Add(torrent.url);
                        #endregion

                        #region UpdateMagnet
                        void UpdateMagnet()
                        {
                            string magnet = $"magnet:?xt=urn:btih:{hex.ToLower()}";

                            if (!string.IsNullOrWhiteSpace(t.Name))
                                magnet += $"&dn={HttpUtility.UrlEncode(t.Name)}";

                            if (t.AnnounceUrls != null && t.AnnounceUrls.Count > 0)
                            {
                                foreach (string announce in t.AnnounceUrls)
                                {
                                    string tr = announce.Contains("/") || announce.Contains(":") ? HttpUtility.UrlEncode(announce) : announce;

                                    if (!magnet.Contains(tr))
                                        magnet += $"&tr={tr}";
                                }
                            }

                            t.torrent.magnet = magnet;
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

                        if (torrent.trackerName != "selezen")
                        {
                            if (torrent.sid > t.torrent.sid)
                                t.torrent.sid = torrent.sid;

                            if (torrent.pir > t.torrent.pir)
                                t.torrent.pir = torrent.pir;
                        }

                        if (torrent.createTime > t.torrent.createTime)
                            t.torrent.createTime = torrent.createTime;

                        if (torrent.voices != null && torrent.voices.Count > 0)
                        {
                            if (t.torrent.voices == null)
                                t.torrent.voices = new HashSet<string>();

                            foreach (var v in torrent.voices)
                                t.torrent.voices.Add(v);
                        }

                        if (torrent.languages != null && torrent.languages.Count > 0)
                        {
                            if (t.torrent.languages == null)
                                t.torrent.languages = new HashSet<string>();

                            foreach (var v in torrent.languages)
                                t.torrent.languages.Add(v);
                        }

                        if (t.torrent.ffprobe == null)
                            t.torrent.ffprobe = torrent.ffprobe;
                    }
                }

                tsort = temp.Select(i => i.Value.torrent);
            }
            else
            {
                tsort = torrents.Values.Where(i => red.trackers == null || red.trackers.Contains(i.trackerName));
            }
            #endregion

            if (apikey == "rus")
                return (tsort.Where(i => i.languages != null && i.languages.Contains("rus") || i.types != null && (i.types.Contains("sport") || i.types.Contains("tvshow") || i.types.Contains("docuserial"))), setcache);

            return (tsort, setcache);
        }
        #endregion

        #region Api
        public static IEnumerable<TorrentDetails> Api(string search, string altname, bool exact, string type, string sort, string tracker, string voice, string videotype, long relased, long quality, long season)
        {
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

            if (string.IsNullOrWhiteSpace(search) || search.Length == 1)
                return new List<TorrentDetails>();

            string _s = StringConvert.SearchName(search);
            string _altsearch = StringConvert.SearchName(altname);

            if (exact)
            {
                #region Точный поиск
                foreach (var mdb in FileDB.masterDb.Where(i => i.Key.StartsWith($"{_s}:") || i.Key.EndsWith($":{_s}") || _altsearch != null && i.Key.Contains(_altsearch)))
                {
                    using (var fdb = FileDB.Open(mdb.Key))
                    {
                        foreach (var t in fdb.Database.Values)
                        {
                            if (t.types == null)
                                continue;

                            if (string.IsNullOrWhiteSpace(type) || t.types.Contains(type))
                            {
                                string _n = StringConvert.SearchName(t.name);
                                string _o = StringConvert.SearchName(t.originalname);

                                if (_n == _s || _o == _s || _altsearch != null && (_n == _altsearch || _o == _altsearch))
                                    AddTorrents(t);
                            }
                        }
                    }
                }
                #endregion
            }
            else
            {
                #region Поиск по совпадению ключа в имени
                var mdb = FileDB.masterDb.Where(i => i.Key.Contains(_s) || _altsearch != null && i.Key.Contains(_altsearch));
                if (!red.evercache.enable || red.evercache.validHour > 0)
                    mdb = mdb.Take(red.maxreadfile);

                foreach (var val in mdb)
                {
                    using (var fdb = FileDB.Open(val.Key))
                    {
                        foreach (var t in fdb.Database.Values)
                        {
                            if (t.types == null)
                                continue;

                            if (string.IsNullOrWhiteSpace(type) || t.types.Contains(type))
                                AddTorrents(t);
                        }
                    }
                }
                #endregion
            }

            if (torrents.Count == 0)
                return new List<TorrentDetails>();

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

            return query.Where(i => red.trackers == null || red.trackers.Contains(i.trackerName));
        }
        #endregion
    }
}

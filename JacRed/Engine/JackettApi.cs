using Jackett;
using JacRed.Controllers;
using JacRed.Models.AppConf;
using System.Reflection;

namespace JacRed.Engine
{
    public static class JackettApi
    {
        static JacConf jackett => ModInit.conf.Jackett;

        #region Indexers
        async public static Task<List<TorrentDetails>> Indexers(string host, string query, string title, string title_original, int year, int is_serial, Dictionary<string, string> category)
        {
            var hybridCache = new HybridCache();

            string mkey = $"JackettApi:{query}:{title}:{year}:{is_serial}";
            if (hybridCache.TryGetValue(mkey, out List<TorrentDetails> cache, inmemory: false))
                return cache;

            var torrents = new ConcurrentBag<TorrentDetails>();

            #region search
            string search = jackett.search_lang == "query" ? query : jackett.search_lang == "title" ? title : title_original;

            if (string.IsNullOrWhiteSpace(search))
            {
                search = query ?? title ?? title_original;
                if (string.IsNullOrWhiteSpace(search))
                    return torrents.ToList();
            }
            #endregion

            #region category
            if (category != null)
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

            #region modpars
            void modpars(List<Task> tasks, string cat)
            {
                if (AppInit.modules != null && AppInit.modules.Count > 0)
                {
                    foreach (var item in AppInit.modules)
                    {
                        foreach (var mod in item.jac)
                        {
                            if (mod.enable)
                            {
                                try
                                {
                                    if (item.assembly.GetType(mod.@namespace) is Type t && t.GetMethod("parsePage") is MethodInfo m)
                                    {
                                        var task = (Task)m.Invoke(null, new object[] { host, torrents, search, cat });
                                        if (task != null)
                                            tasks.Add(task);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            #endregion

            #region Парсим торренты
            if (is_serial == 1)
            {
                #region Фильм
                var tasks = new List<Task>
                {
                    RutorController.search(host, torrents, search, "1"),  // movie
                    RutorController.search(host, torrents, search, "5"),  // movie
                    RutorController.search(host, torrents, search, "7"),  // multfilm
                    RutorController.search(host, torrents, search, "12"), // documovie
                    RutorController.search(host, torrents, search, "17", true, "1"), // UKR

                    MegapeerController.search(host, torrents, search, "79"),  // Наши фильмы
                    MegapeerController.search(host, torrents, search, "80"),  // Зарубежные фильмы
                    MegapeerController.search(host, torrents, search, "76"),  // Мультипликация

                    TorrentByController.search(host, torrents, search, "1"), // movie
                    TorrentByController.search(host, torrents, search, "2"), // movie
                    TorrentByController.search(host, torrents, search, "5"), // multfilm

                    KinozalController.search(host, torrents, search, new string[] { "movie", "multfilm", "tvshow" }),
                    NNMClubController.search(host, torrents, search, new string[] { "movie", "multfilm", "documovie" }),
                    TolokaController.search(host, torrents, search, new string[] { "movie", "multfilm", "documovie" }),
                    RutrackerController.search(host, torrents, search, new string[] { "movie", "multfilm", "documovie" }),
                    BitruController.search(host, torrents, search, new string[] { "movie" }),
                    SelezenController.search(host, torrents, search),
                    BigFanGroup.search(host, torrents, search, new string[] { "movie", "multfilm", "documovie" })
                };

                modpars(tasks, "movie");

                await Task.WhenAll(tasks);
                #endregion
            }
            else if (is_serial == 2)
            {
                #region Сериал
                var tasks = new List<Task>
                {
                    RutorController.search(host, torrents, search, "4"),  // serial
                    RutorController.search(host, torrents, search, "16"), // serial
                    RutorController.search(host, torrents, search, "7"),  // multserial
                    RutorController.search(host, torrents, search, "12"), // docuserial
                    RutorController.search(host, torrents, search, "6"),  // tvshow
                    RutorController.search(host, torrents, search, "17", true, "4"), // UKR

                    MegapeerController.search(host, torrents, search, "5"),  // serial
                    MegapeerController.search(host, torrents, search, "6"),  // serial
                    MegapeerController.search(host, torrents, search, "55"), // docuserial
                    MegapeerController.search(host, torrents, search, "57"), // tvshow
                    MegapeerController.search(host, torrents, search, "76"), // multserial

                    TorrentByController.search(host, torrents, search, "3"),  // serial
                    TorrentByController.search(host, torrents, search, "5"),  // multserial
                    TorrentByController.search(host, torrents, search, "4"),  // tvshow
                    TorrentByController.search(host, torrents, search, "12"), // tvshow

                    KinozalController.search(host, torrents, search, new string[] { "serial", "multserial", "tvshow" }),
                    NNMClubController.search(host, torrents, search, new string[] { "serial", "multserial", "docuserial" }),
                    TolokaController.search(host, torrents, search, new string[] { "serial", "multserial", "docuserial" }),
                    RutrackerController.search(host, torrents, search, new string[] { "serial", "multserial", "docuserial" }),
                    BitruController.search(host, torrents, search, new string[] { "serial" }),
                    LostfilmController.search(host, torrents, search),
                    BigFanGroup.search(host, torrents, search, new string[] { "serial", "multserial", "docuserial", "tvshow" })
                };

                modpars(tasks, "serial");

                await Task.WhenAll(tasks);
                #endregion
            }
            else if (is_serial == 3)
            {
                #region tvshow
                var tasks = new List<Task>
                {
                    RutorController.search(host, torrents, search, "6"),
                    MegapeerController.search(host, torrents, search, "57"),
                    TorrentByController.search(host, torrents, search, "4"),
                    TorrentByController.search(host, torrents, search, "12"),
                    KinozalController.search(host, torrents, search, new string[] { "tvshow" }),
                    NNMClubController.search(host, torrents, search, new string[] { "docuserial", "documovie" }),
                    TolokaController.search(host, torrents, search, new string[] { "docuserial", "documovie" }),
                    RutrackerController.search(host, torrents, search, new string[] { "tvshow" }),
                    BigFanGroup.search(host, torrents, search, new string[] { "tvshow" })
                };

                modpars(tasks, "tvshow");

                await Task.WhenAll(tasks);
                #endregion
            }
            else if (is_serial == 4)
            {
                #region docuserial / documovie
                var tasks = new List<Task>
                {
                    RutorController.search(host, torrents, search, "12"),
                    MegapeerController.search(host, torrents, search, "55"),
                    NNMClubController.search(host, torrents, search, new string[] { "docuserial", "documovie" }),
                    TolokaController.search(host, torrents, search, new string[] { "docuserial", "documovie" }),
                    RutrackerController.search(host, torrents, search, new string[] { "docuserial", "documovie" }),
                    BigFanGroup.search(host, torrents, search, new string[] { "docuserial", "documovie" })
                };

                modpars(tasks, "documental");

                await Task.WhenAll(tasks);
                #endregion
            }
            else if (is_serial == 5)
            {
                #region anime
                string animesearch = title ?? query;

                var tasks = new List<Task>
                {
                    RutorController.search(host, torrents, animesearch, "10"),
                    TorrentByController.search(host, torrents, animesearch, "6"),
                    KinozalController.search(host, torrents, animesearch, new string[] { "anime" }),
                    NNMClubController.search(host, torrents, animesearch, new string[] { "anime" }),
                    RutrackerController.search(host, torrents, animesearch, new string[] { "anime" }),
                    TolokaController.search(host, torrents, search, new string[] { "anime" }),
                    AniLibriaController.search(host, torrents, animesearch),
                    AnimeLayerController.search(host, torrents, animesearch),
                    AnifilmController.search(host, torrents, animesearch)
                };

                modpars(tasks, "anime");

                await Task.WhenAll(tasks);
                #endregion
            }
            else
            {
                #region Неизвестно
                var tasks = new List<Task>
                {
                    RutorController.search(host, torrents, search, "0"),
                    MegapeerController.search(host, torrents, search, "0"),
                    TorrentByController.search(host, torrents, search, "0"),
                    KinozalController.search(host, torrents, search, null),
                    NNMClubController.search(host, torrents, search, null),
                    BitruController.search(host, torrents, search, null),
                    RutrackerController.search(host, torrents, search, null),
                    TolokaController.search(host, torrents, search, null),
                    AniLibriaController.search(host, torrents, search),
                    AnimeLayerController.search(host, torrents, search),
                    AnifilmController.search(host, torrents, search),
                    SelezenController.search(host, torrents, search),
                    LostfilmController.search(host, torrents, search),
                    BigFanGroup.search(host, torrents, search, null)
                };

                modpars(tasks, "search");

                await Task.WhenAll(tasks);
                #endregion
            }
            #endregion

            var hash = new HashSet<string>();
            var finaly = new List<TorrentDetails>(torrents.Count);

            foreach (var t in torrents)
            {
                if (t.trackerName == null)
                    t.trackerName = Regex.Match(t.url, "https?://([^/]+)").Groups[1].Value;

                if (!string.IsNullOrEmpty(ModInit.conf.filter) && !Regex.IsMatch(t.title, ModInit.conf.filter, RegexOptions.IgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(ModInit.conf.filter_ignore) && Regex.IsMatch(t.title, ModInit.conf.filter_ignore, RegexOptions.IgnoreCase))
                    continue;

                if (!hash.Contains(t.url))
                {
                    hash.Add(t.url);
                    finaly.Add(t);
                }
            }

            var result = finaly.AsEnumerable();

            if (is_serial == 1 && year > 0)
                result = result.Where(i => i.title.Contains(year.ToString()) || i.title.Contains($"{year+1}") || i.title.Contains($"{year-1}"));

            if (ModInit.conf.Jackett.cacheToMinutes > 0)
                hybridCache.Set(mkey, result.ToList(), DateTime.Now.AddMinutes(ModInit.conf.Jackett.cacheToMinutes), inmemory: false);

            return result.ToList();
        }
        #endregion

        #region Api
        public static Task<List<TorrentDetails>> Api(string host, string search)
        {
            return Indexers(host, search, null, null, 0, 0, null);
        }
        #endregion
    }
}

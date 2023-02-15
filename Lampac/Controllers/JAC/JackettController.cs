using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Lampac.Engine.CORE;
using System.Text.RegularExpressions;
using Lampac.Engine;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Lampac.Models.JAC;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Caching.Memory;
using System;
using Lampac.Controllers.CRON;

namespace Lampac.Controllers.JAC
{
    public class JackettController : BaseController
    {
        #region JacRed
        [Route("api/v1.0/conf")]
        public JsonResult JacRedConf(string apikey)
        {
            return Json(new
            {
                apikey = string.IsNullOrWhiteSpace(AppInit.conf.jac.apikey) || apikey == AppInit.conf.jac.apikey
            });
        }

        [Route("api/v1.0/torrents")]
        async public Task<JsonResult> JacRed(string apikey, string search)
        {
            #region search kp/imdb
            if (!string.IsNullOrWhiteSpace(search) && Regex.IsMatch(search.Trim(), "^(tt|kp)[0-9]+$"))
            {
                string memkey = $"api/v1.0/torrents:{search}";
                if (!memoryCache.TryGetValue(memkey, out string original_name))
                {
                    search = search.Trim();
                    string uri = $"&imdb={search}";
                    if (search.StartsWith("kp"))
                        uri = $"&kp={search.Remove(0, 2)}";

                    var root = await HttpClient.Get<JObject>("https://api.alloha.tv/?token=04941a9a3ca3ac16e2b4327347bbc1" + uri, timeoutSeconds: 8);
                    original_name = root?.Value<JObject>("data")?.Value<string>("original_name");

                    memoryCache.Set(memkey, original_name ?? string.Empty, DateTime.Now.AddDays(1));
                }

                search = original_name;
            }
            #endregion

            #region getSizeInfo
            long getSizeInfo(string sizeName)
            {
                if (string.IsNullOrWhiteSpace(sizeName))
                    return 0;

                try
                {
                    double size = 0.1;
                    var gsize = Regex.Match(sizeName, "([0-9\\.,]+) (Mb|МБ|GB|ГБ|TB|ТБ)", RegexOptions.IgnoreCase).Groups;
                    if (!string.IsNullOrWhiteSpace(gsize[2].Value))
                    {
                        if (double.TryParse(gsize[1].Value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out size) && size != 0)
                        {
                            if (gsize[2].Value.ToLower() is "gb" or "гб")
                                size *= 1024;

                            if (gsize[2].Value.ToLower() is "tb" or "тб")
                                size *= 1048576;

                            return (long)(size * 1048576);
                        }
                    }
                }
                catch { }

                return 0;
            }
            #endregion

            var torrents = await Torrents(search, null, null, 0, 0, null);

            return Json(torrents.Select(i => new
            {
                tracker = i.trackerName,
                url = i.url != null && i.url.StartsWith("http") ? i.url : null,
                i.title,
                size = getSizeInfo(i.sizeName),
                i.sizeName,
                i.createTime,
                i.sid,
                i.pir,
                magnet = i.magnet ?? $"{i.parselink}&apikey={apikey}",
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

        #region Jackett
        [Route("api/v2.0/indexers/{status}/results")]
        async public Task<ActionResult> Jackett(string apikey, string query, string title, string title_original, int year, int is_serial, Dictionary<string, string> category)
        {
            var torrents = await Torrents(query, title, title_original, year, is_serial, category);

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

            #region getSizeInfo
            long getSizeInfo(string sizeName)
            {
                if (string.IsNullOrWhiteSpace(sizeName))
                    return 0;

                try
                {
                    double size = 0.1;
                    var gsize = Regex.Match(sizeName, "([0-9\\.,]+) (Mb|МБ|GB|ГБ|TB|ТБ)", RegexOptions.IgnoreCase).Groups;
                    if (!string.IsNullOrWhiteSpace(gsize[2].Value))
                    {
                        if (double.TryParse(gsize[1].Value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out size) && size != 0)
                        {
                            if (gsize[2].Value.ToLower() is "gb" or "гб")
                                size *= 1024;

                            if (gsize[2].Value.ToLower() is "tb" or "тб")
                                size *= 1048576;

                            return (long)(size * 1048576);
                        }
                    }
                }
                catch { }

                return 0;
            }
            #endregion

            return Content(JsonConvert.SerializeObject(new
            {
                Results = torrents.Where(i => i.sid > 0).OrderByDescending(i => i.createTime).Select(i => new
                {
                    Tracker = i.trackerName,
                    Details = i.url != null && i.url.StartsWith("http") ? i.url : null,
                    Title = i.title,
                    Size = getSizeInfo(i.sizeName),
                    PublishDate = i.createTime,
                    Category = getCategoryIds(i, out string categoryDesc),
                    CategoryDesc = categoryDesc,
                    Seeders = i.sid,
                    Peers = i.pir,
                    MagnetUri = i.magnet,
                    Link = i.parselink != null ? $"{i.parselink}&apikey={apikey}" : null,
                    Info = new
                    {
                        i.name,
                        i.originalname,
                        i.relased,
                        i.quality,
                        i.videotype,
                        i.sizeName,
                        i.voices,
                        i.seasons
                    }
                })
            }), contentType: "application/json; charset=utf-8");
        }
        #endregion


        #region Torrents
        async ValueTask<List<TorrentDetails>> Torrents(string query, string title, string title_original, int year, int is_serial, Dictionary<string, string> category)
        {
            var torrents = new ConcurrentBag<TorrentDetails>();
            var temptorrents = new ConcurrentBag<TorrentDetails>();

            #region search
            string search = AppInit.conf.jac.search_lang == "query" ? query : AppInit.conf.jac.search_lang == "title" ? title : title_original;

            if (string.IsNullOrWhiteSpace(search))
            {
                search = title_original ?? title ?? query;
                if (string.IsNullOrWhiteSpace(search))
                    goto end;
            }
            #endregion

            #region Запрос с NUM
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(title_original))
            {
                var mNum = Regex.Match(query ?? string.Empty, "^([^a-z-A-Z]+) ([^а-я-А-Я]+) ([0-9]{4})$");

                if (mNum.Success && Regex.IsMatch(mNum.Groups[2].Value, "[a-zA-Z]{4}"))
                {
                    var g = mNum.Groups;

                    title = g[1].Value;
                    title_original = g[2].Value;
                    year = int.Parse(g[3].Value);

                    search = AppInit.conf.jac.search_lang == "query" ? query : AppInit.conf.jac.search_lang == "title" ? title : title_original;
                }
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

            #region Парсим торренты
            if (is_serial == 1)
            {
                #region Фильм
                await Task.WhenAll(new Task[]
                {
                    RutorController.parsePage(host, temptorrents, search, "1"),  // movie
                    RutorController.parsePage(host, temptorrents, search, "5"),  // movie
                    RutorController.parsePage(host, temptorrents, search, "7"),  // multfilm
                    RutorController.parsePage(host, temptorrents, search, "12"), // documovie
                    RutorController.parsePage(host, temptorrents, search, "17", true, "1"), // UKR

                    MegapeerController.parsePage(host, temptorrents, search, "79"),  // movie
                    MegapeerController.parsePage(host, temptorrents, search, "174"), // movie
                    MegapeerController.parsePage(host, temptorrents, search, "76"),  // multfilm

                    TorrentByController.parsePage(host, temptorrents, search, "1"), // movie
                    TorrentByController.parsePage(host, temptorrents, search, "2"), // movie
                    TorrentByController.parsePage(host, temptorrents, search, "5"), // multfilm

                    KinozalController.parsePage(host, temptorrents, search, new string[] { "movie", "multfilm", "tvshow" }),
                    NNMClubController.parsePage(host, temptorrents, search, new string[] { "movie", "multfilm", "documovie" }),
                    TolokaController.parsePage(host, temptorrents, search, new string[] { "movie", "multfilm", "documovie" }),
                    RutrackerController.parsePage(host, temptorrents, search, new string[] { "movie", "multfilm", "documovie" }),
                    UnderverseController.parsePage(temptorrents, search, new string[] { "movie", "multfilm", "documovie" }),
                    BitruController.parsePage(host, temptorrents, search, new string[] { "movie" }),
                    SelezenController.parsePage(host, temptorrents, search),

                });
                #endregion
            }
            else if (is_serial == 2)
            {
                #region Сериал
                await Task.WhenAll(new Task[]
                {
                    RutorController.parsePage(host, temptorrents, search, "4"),  // serial
                    RutorController.parsePage(host, temptorrents, search, "16"), // serial
                    RutorController.parsePage(host, temptorrents, search, "7"),  // multserial
                    RutorController.parsePage(host, temptorrents, search, "12"), // docuserial
                    RutorController.parsePage(host, temptorrents, search, "6"),  // tvshow
                    RutorController.parsePage(host, temptorrents, search, "17", true, "4"), // UKR

                    MegapeerController.parsePage(host, temptorrents, search, "5"),  // serial
                    MegapeerController.parsePage(host, temptorrents, search, "6"),  // serial
                    MegapeerController.parsePage(host, temptorrents, search, "55"), // docuserial
                    MegapeerController.parsePage(host, temptorrents, search, "57"), // tvshow
                    MegapeerController.parsePage(host, temptorrents, search, "76"), // multserial

                    TorrentByController.parsePage(host, temptorrents, search, "3"),  // serial
                    TorrentByController.parsePage(host, temptorrents, search, "5"),  // multserial
                    TorrentByController.parsePage(host, temptorrents, search, "4"),  // tvshow
                    TorrentByController.parsePage(host, temptorrents, search, "12"), // tvshow

                    KinozalController.parsePage(host, temptorrents, search, new string[] { "serial", "multserial", "tvshow" }),
                    NNMClubController.parsePage(host, temptorrents, search, new string[] { "serial", "multserial", "docuserial" }),
                    TolokaController.parsePage(host, temptorrents, search, new string[] { "serial", "multserial", "docuserial" }),
                    RutrackerController.parsePage(host, temptorrents, search, new string[] { "serial", "multserial", "docuserial" }),
                    UnderverseController.parsePage(temptorrents, search, new string[] { "serial", "docuserial" }),
                    BitruController.parsePage(host, temptorrents, search, new string[] { "serial" }),
                    LostfilmController.parsePage(host, temptorrents, search),

                });
                #endregion
            }
            else if (is_serial == 3)
            {
                #region tvshow
                await Task.WhenAll(new Task[]
                {
                    RutorController.parsePage(host, temptorrents, search, "6"),
                    MegapeerController.parsePage(host, temptorrents, search, "57"),
                    TorrentByController.parsePage(host, temptorrents, search, "4"),
                    TorrentByController.parsePage(host, temptorrents, search, "12"),
                    KinozalController.parsePage(host, temptorrents, search, new string[] { "tvshow" }),
                    NNMClubController.parsePage(host, temptorrents, search, new string[] { "docuserial", "documovie" }),
                    TolokaController.parsePage(host, temptorrents, search, new string[] { "docuserial", "documovie" }),
                    RutrackerController.parsePage(host, temptorrents, search, new string[] { "tvshow" }),
                    UnderverseController.parsePage(temptorrents, search, new string[] { "tvshow" }),

                });
                #endregion
            }
            else if (is_serial == 4)
            {
                #region docuserial / documovie
                await Task.WhenAll(new Task[]
                {
                    RutorController.parsePage(host, temptorrents, search, "12"),
                    MegapeerController.parsePage(host, temptorrents, search, "55"),
                    NNMClubController.parsePage(host, temptorrents, search, new string[] { "docuserial", "documovie" }),
                    TolokaController.parsePage(host, temptorrents, search, new string[] { "docuserial", "documovie" }),
                    RutrackerController.parsePage(host, temptorrents, search, new string[] { "docuserial", "documovie" }),
                    UnderverseController.parsePage(temptorrents, search, new string[] { "docuserial", "documovie" }),

                });
                #endregion
            }
            else if (is_serial == 5)
            {
                #region anime
                string animesearch = title ?? query;

                await Task.WhenAll(new Task[]
                {
                    RutorController.parsePage(host, temptorrents, animesearch, "10"),
                    TorrentByController.parsePage(host, temptorrents, animesearch, "6"),
                    KinozalController.parsePage(host, temptorrents, animesearch, new string[] { "anime" }),
                    NNMClubController.parsePage(host, temptorrents, animesearch, new string[] { "anime" }),
                    RutrackerController.parsePage(host, temptorrents, animesearch, new string[] { "anime" }),
                    AniLibriaController.parsePage(host, temptorrents, animesearch),
                    AnimeLayerController.parsePage(host, temptorrents, animesearch),
                    AnifilmController.parsePage(host, temptorrents, animesearch),

                });
                #endregion
            }
            else
            {
                #region Неизвестно
                await Task.WhenAll(new Task[]
                {
                    RutorController.parsePage(host, temptorrents, search, "0"),
                    MegapeerController.parsePage(host, temptorrents, search, "0"),
                    TorrentByController.parsePage(host, temptorrents, search, "0"),
                    KinozalController.parsePage(host, temptorrents, search, null),
                    NNMClubController.parsePage(host, temptorrents, search, null),
                    TolokaController.parsePage(host, temptorrents, search, null),
                    RutrackerController.parsePage(host, temptorrents, search, null),
                    UnderverseController.parsePage(temptorrents, search, null),
                    SelezenController.parsePage(host, temptorrents, search),
                    BitruController.parsePage(host, temptorrents, search, null),
                    AniLibriaController.parsePage(host, temptorrents, search),
                    AnimeLayerController.parsePage(host, temptorrents, search),
                    AnifilmController.parsePage(host, temptorrents, search),
                    LostfilmController.parsePage(host, temptorrents, search),
                });
                #endregion
            }
            #endregion

            if (is_serial > 0 && (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(title_original)))
            {
                #region Точный поиск
                string _n = StringConvert.SearchName(title);
                string _o = StringConvert.SearchName(title_original);

                foreach (var t in temptorrents)
                {
                    string name = StringConvert.SearchName(t.name);
                    string originalname = StringConvert.SearchName(t.originalname);

                    // Точная выборка по name или originalname
                    if (_n != null && _n == name || _o != null && _o == originalname)
                    {
                        if (is_serial == 1)
                        {
                            #region Фильм
                            if (year > 0)
                            {
                                if (t.relased == year || t.relased == year - 1 || t.relased == year + 1)
                                    torrents.Add(t);
                            }
                            else
                            {
                                torrents.Add(t);
                            }
                            #endregion
                        }
                        else if (is_serial == 2)
                        {
                            #region Сериал
                            if (year > 0)
                            {
                                if (t.relased >= year - 1)
                                    torrents.Add(t);
                            }
                            else
                            {
                                torrents.Add(t);
                            }
                            #endregion
                        }
                        else if (is_serial == 3)
                        {
                            #region tvshow
                            if (year > 0)
                            {
                                if (t.relased >= year - 1)
                                    torrents.Add(t);
                            }
                            else
                            {
                                torrents.Add(t);
                            }
                            #endregion
                        }
                        else if (is_serial == 4)
                        {
                            #region docuserial / documovie
                            if (year > 0)
                            {
                                if (t.relased >= year - 1)
                                    torrents.Add(t);
                            }
                            else
                            {
                                torrents.Add(t);
                            }
                            #endregion
                        }
                        else if (is_serial == 5)
                        {
                            #region anime
                            if (year > 0)
                            {
                                if (t.relased >= year - 1)
                                    torrents.Add(t);
                            }
                            else
                            {
                                torrents.Add(t);
                            }
                            #endregion
                        }
                        else
                        {
                            #region Неизвестно
                            if (year > 0)
                            {
                                if (t.relased >= year - 1)
                                    torrents.Add(t);
                            }
                            else
                            {
                                torrents.Add(t);
                            }
                            #endregion
                        }
                    }
                }
                #endregion
            }
            else
            {
                // Обычный поиск
                torrents = temptorrents;
            }

            foreach (var t in torrents)
            {
                #region quality
                t.quality = 480;

                if (t.quality == 480)
                {
                    if (t.title.Contains("720p"))
                    {
                        t.quality = 720;
                    }
                    else if (t.title.Contains("1080p"))
                    {
                        t.quality = 1080;
                    }
                    else if (Regex.IsMatch(t.title.ToLower(), "(4k|uhd)( |\\]|,|$)") || t.title.Contains("2160p"))
                    {
                        // Вышел после 2000г
                        // Размер файла выше 10GB
                        // Есть пометка о 4K
                        t.quality = 2160;
                    }
                }
                #endregion

                #region videotype
                t.videotype = "sdr";
                if (Regex.IsMatch(t.title.ToLower(), "(\\[| )hdr( |\\]|,|$)") || Regex.IsMatch(t.title.ToLower(), "(10-bit|10 bit|10-бит|10 бит)"))
                {
                    t.videotype = "hdr";
                }
                #endregion

                #region voice
                t.voices = new HashSet<string>();

                if (t.trackerName == "lostfilm")
                {
                    t.voices.Add("LostFilm");
                }
                else if (t.trackerName == "toloka")
                {
                    t.voices.Add("Украинский");
                }
                else if (t.trackerName == "hdrezka")
                {
                    t.voices.Add("HDRezka");
                }
                else
                {
                    var allVoices = new HashSet<string>
                    {
                        "Ozz", "Laci", "Kerob", "LE-Production",  "Parovoz Production", "Paradox", "Omskbird", "LostFilm", "Причудики", "BaibaKo", "NewStudio", "AlexFilm", "FocusStudio", "Gears Media", "Jaskier", "ViruseProject",
                        "Кубик в Кубе", "IdeaFilm", "Sunshine Studio", "Ozz.tv", "Hamster Studio", "Сербин", "To4ka", "Кравец", "Victory-Films", "SNK-TV", "GladiolusTV", "Jetvis Studio", "ApofysTeam", "ColdFilm",
                        "Agatha Studdio", "KinoView", "Jimmy J.", "Shadow Dub Project", "Amedia", "Red Media", "Selena International", "Гоблин", "Universal Russia", "Kiitos", "Paramount Comedy", "Кураж-Бамбей",
                        "Студия Пиратского Дубляжа", "Чадов", "Карповский", "RecentFilms", "Первый канал", "Alternative Production", "NEON Studio", "Колобок", "Дольский", "Синема УС", "Гаврилов", "Живов", "SDI Media",
                        "Алексеев", "GreenРай Studio", "Михалев", "Есарев", "Визгунов", "Либергал", "Кузнецов", "Санаев", "ДТВ", "Дохалов", "Sunshine Studio", "Горчаков", "LevshaFilm", "CasStudio", "Володарский",
                        "ColdFilm", "Шварко", "Карцев", "ETV+", "ВГТРК", "Gravi-TV", "1001cinema", "Zone Vision Studio", "Хихикающий доктор", "Murzilka", "turok1990", "FOX", "STEPonee", "Elrom", "Колобок", "HighHopes",
                        "SoftBox", "GreenРай Studio", "NovaFilm", "Четыре в квадрате", "Greb&Creative", "MUZOBOZ", "ZM-Show", "RecentFilms", "Kerems13", "Hamster Studio", "New Dream Media", "Игмар", "Котов", "DeadLine Studio",
                        "Jetvis Studio", "РенТВ", "Андрей Питерский", "Fox Life", "Рыбин", "Trdlo.studio", "Studio Victory Аsia", "Ozeon", "НТВ", "CP Digital", "AniLibria", "STEPonee", "Levelin", "FanStudio", "Cmert",
                        "Интерфильм", "SunshineStudio", "Kulzvuk Studio", "Кашкин", "Вартан Дохалов", "Немахов", "Sedorelli", "СТС", "Яроцкий", "ICG", "ТВЦ", "Штейн", "AzOnFilm", "SorzTeam", "Гаевский", "Мудров",
                        "Воробьев Сергей", "Студия Райдо", "DeeAFilm Studio", "zamez", "ViruseProject", "Иванов", "STEPonee", "РенТВ", "СВ-Дубль", "BadBajo", "Комедия ТВ", "Мастер Тэйп", "5-й канал СПб", "SDI Media",
                        "Гланц", "Ох! Студия", "СВ-Кадр", "2x2", "Котова", "Позитив", "RusFilm", "Назаров", "XDUB Dorama", "Реальный перевод", "Kansai", "Sound-Group", "Николай Дроздов", "ZEE TV", "Ozz.tv", "MTV",
                        "Сыендук", "GoldTeam", "Белов", "Dream Records", "Яковлев", "Vano", "SilverSnow", "Lord32x", "Filiza Studio", "Sony Sci-Fi", "Flux-Team", "NewStation", "XDUB Dorama", "Hamster Studio", "Dream Records",
                        "DexterTV", "ColdFilm", "Good People", "RusFilm", "Levelin", "AniDUB", "SHIZA Project", "AniLibria.TV", "StudioBand", "AniMedia", "Kansai", "Onibaku", "JWA Project", "MC Entertainment", "Oni", "Jade",
                        "Ancord", "ANIvoice", "Nika Lenina", "Bars MacAdams", "JAM", "Anika", "Berial", "Kobayashi", "Cuba77", "RiZZ_fisher", "OSLIKt", "Lupin", "Ryc99", "Nazel & Freya", "Trina_D", "JeFerSon", "Vulpes Vulpes",
                        "Hamster", "KinoGolos", "Fox Crime", "Денис Шадинский", "AniFilm", "Rain Death", "LostFilm", "New Records", "Ancord", "Первый ТВЧ", "RG.Paravozik", "Profix Media", "Tycoon", "RealFake",
                        "HDrezka", "Jimmy J.", "AlexFilm", "Discovery", "Viasat History", "AniMedia", "JAM", "HiWayGrope", "Ancord", "СВ-Дубль", "Tycoon", "SHIZA Project", "GREEN TEA", "STEPonee", "AlphaProject",
                        "AnimeReactor", "Animegroup", "Shachiburi", "Persona99", "3df voice", "CactusTeam", "AniMaunt", "AniMedia", "AnimeReactor", "ShinkaDan", "Jaskier", "ShowJet", "RAIM", "RusFilm", "Victory-Films",
                        "АрхиТеатр", "Project Web Mania", "ko136", "КураСгречей", "AMS", "СВ-Студия", "Храм Дорам ТВ", "TurkStar", "Медведев", "Рябов", "BukeDub", "FilmGate", "FilmsClub", "Sony Turbo", "ТВЦ", "AXN Sci-Fi",
                        "NovaFilm", "DIVA Universal", "Курдов", "Неоклассика", "fiendover", "SomeWax", "Логинофф", "Cartoon Network", "Sony Turbo", "Loginoff", "CrezaStudio", "Воротилин", "LakeFilms", "Andy", "CP Digital",
                        "XDUB Dorama + Колобок", "SDI Media", "KosharaSerials", "Екатеринбург Арт", "Julia Prosenuk", "АРК-ТВ Studio", "Т.О Друзей", "Anifilm", "Animedub", "AlphaProject", "Paramount Channel", "Кириллица",
                        "AniPLague", "Видеосервис", "JoyStudio", "HighHopes", "TVShows", "AniFilm", "GostFilm", "West Video", "Формат AB", "Film Prestige", "West Video", "Екатеринбург Арт", "SovetRomantica", "РуФилмс",
                        "AveBrasil", "Greb&Creative", "BTI Studios", "Пифагор", "Eurochannel", "NewStudio", "Кармен Видео", "Кошкин", "Кравец", "Rainbow World", "Воротилин", "Варус-Видео", "ClubFATE", "HiWay Grope",
                        "Banyan Studio", "Mallorn Studio", "Asian Miracle Group", "Эй Би Видео", "AniStar", "Korean Craze", "LakeFilms", "Невафильм", "Hallmark", "Netflix", "Mallorn Studio", "Sony Channel", "East Dream",
                        "Bonsai Studio", "Lucky Production", "Octopus", "TUMBLER Studio", "CrazyCatStudio", "Amber", "Train Studio", "Анастасия Гайдаржи", "Мадлен Дюваль", "Fox Life", "Sound Film", "Cowabunga Studio", "Фильмэкспорт",
                        "VO-Production", "Sound Film", "Nickelodeon", "MixFilm", "GreenРай Studio", "Sound-Group", "Back Board Cinema", "Кирилл Сагач", "Bonsai Studio", "Stevie", "OnisFilms", "MaxMeister", "Syfy Universal",
                        "TUMBLER Studio", "NewStation", "Neo-Sound", "Муравский", "IdeaFilm", "Рутилов", "Тимофеев", "Лагута", "Дьяконов", "Zone Vision Studio", "Onibaku", "AniMaunt", "Voice Project", "AniStar", "Пифагор",
                        "VoicePower", "StudioFilms", "Elysium", "AniStar", "BeniAffet", "Selena International", "Paul Bunyan", "CoralMedia", "Кондор", "Игмар", "ViP Premiere", "FireDub", "AveTurk", "Sony Sci-Fi", "Янкелевич",
                        "Киреев", "Багичев", "2x2", "Лексикон", "Нота", "Arisu", "Superbit", "AveDorama", "VideoBIZ", "Киномания", "DDV", "Alternative Production", "WestFilm", "Анастасия Гайдаржи + Андрей Юрченко", "Киномания",
                        "Agatha Studdio", "GreenРай Studio", "VSI Moscow", "Horizon Studio", "Flarrow Films", "Amazing Dubbing", "Asian Miracle Group", "Видеопродакшн", "VGM Studio", "FocusX", "CBS Drama", "NovaFilm", "Novamedia",
                        "East Dream", "Дасевич", "Анатолий Гусев", "Twister", "Морозов", "NewComers", "kubik&ko", "DeMon", "Анатолий Ашмарин", "Inter Video", "Пронин", "AMC", "Велес", "Volume-6 Studio", "Хоррор Мэйкер",
                        "Ghostface", "Sephiroth", "Акира", "Деваль Видео", "RussianGuy27", "neko64", "Shaman", "Franek Monk", "Ворон", "Andre1288", "Selena International", "GalVid", "Другое кино", "Студия NLS", "Sam2007",
                        "HaseRiLLoPaW", "Севастьянов", "D.I.M.", "Марченко", "Журавлев", "Н-Кино", "Lazer Video", "SesDizi", "Red Media", "Рудой", "Товбин", "Сергей Дидок", "Хуан Рохас", "binjak", "Карусель", "Lizard Cinema",
                        "Варус-Видео", "Акцент", "RG.Paravozik", "Max Nabokov", "Barin101", "Васька Куролесов", "Фортуна-Фильм", "Amalgama", "AnyFilm", "Студия Райдо", "Козлов", "Zoomvision Studio", "Пифагор", "Urasiko",
                        "VIP Serial HD", "НСТ", "Кинолюкс", "Project Web Mania", "Завгородний", "AB-Video", "Twister", "Universal Channel", "Wakanim", "SnowRecords", "С.Р.И", "Старый Бильбо", "Ozz.tv", "Mystery Film", "РенТВ",
                        "Латышев", "Ващенко", "Лайко", "Сонотек", "Psychotronic", "DIVA Universal", "Gremlin Creative Studio", "Нева-1", "Максим Жолобов", "Good People", "Мобильное телевидение", "Lazer Video",
                        "IVI", "DoubleRec", "Milvus", "RedDiamond Studio", "Astana TV", "Никитин", "КТК", "D2Lab", "НСТ", "DoubleRec", "Black Street Records", "Останкино", "TatamiFilm", "Видеобаза", "Crunchyroll", "Novamedia",
                        "RedRussian1337", "КонтентикOFF", "Creative Sound", "HelloMickey Production", "Пирамида", "CLS Media", "Сонькин", "Мастер Тэйп", "Garsu Pasaulis", "DDV", "IdeaFilm", "Gold Cinema", "Че!", "Нарышкин",
                        "Intra Communications", "OnisFilms", "XDUB Dorama", "Кипарис", "Королёв", "visanti-vasaer", "Готлиб", "Paramount Channel", "СТС", "диктор CDV", "Pazl Voice", "Прямостанов", "Zerzia", "НТВ", "MGM",
                        "Дьяков", "Вольга", "АРК-ТВ Studio", "Дубровин", "МИР", "Netflix", "Jetix", "Кипарис", "RUSCICO", "Seoul Bay", "Филонов", "Махонько", "Строев", "Саня Белый", "Говинда Рага", "Ошурков", "Horror Maker",
                        "Хлопушка", "Хрусталев", "Антонов Николай", "Золотухин", "АрхиАзия", "Попов", "Ultradox", "Мост-Видео", "Альтера Парс", "Огородников", "Твин", "Хабар", "AimaksaLTV", "ТНТ", "FDV", "3df voice",
                        "The Kitchen Russia", "Ульпаней Эльром", "Видеоимпульс", "GoodTime Media", "Alezan", "True Dubbing Studio", "FDV", "Карусель", "Интер", "Contentica", "Мельница", "RealFake", "ИДДК", "Инфо-фильм",
                        "Мьюзик-трейд", "Кирдин | Stalk", "ДиоНиК", "Стасюк", "TV1000", "Hallmark", "Тоникс Медиа", "Бессонов", "Gears Media", "Бахурани", "NewDub", "Cinema Prestige", "Набиев", "New Dream Media", "ТВ3",
                        "Малиновский Сергей", "Superbit", "Кенс Матвей", "LE-Production", "Voiz", "Светла", "Cinema Prestige", "JAM", "LDV", "Videogram", "Индия ТВ", "RedDiamond Studio", "Герусов", "Элегия фильм", "Nastia",
                        "Семыкина Юлия", "Электричка", "Штамп Дмитрий", "Пятница", "Oneinchnales", "Gravi-TV", "D2Lab", "Кинопремьера", "Бусов Глеб", "LE-Production", "1001cinema", "Amazing Dubbing", "Emslie",
                        "1+1", "100 ТВ", "1001 cinema", "2+2", "2х2", "3df voice", "4u2ges", "5 канал", "A. Lazarchuk", "AAA-Sound", "AB-Video", "AdiSound", "ALEKS KV", "AlexFilm", "AlphaProject", "Alternative Production",
                        "Amalgam", "AMC", "Amedia", "AMS", "Andy", "AniLibria", "AniMedia", "Animegroup", "Animereactor", "AnimeSpace Team", "Anistar", "AniUA", "AniWayt", "Anything-group", "AOS",
                        "Arasi project", "ARRU Workshop", "AuraFilm", "AvePremier", "AveTurk", "AXN Sci-Fi", "Azazel", "AzOnFilm", "BadBajo", "BadCatStudio", "BBC Saint-Petersburg", "BD CEE", "Black Street Records",
                        "Bonsai Studio", "Boльгa", "Brain Production", "BraveSound", "BTI Studios", "Bubble Dubbing Company", "Byako Records", "Cactus Team", "Cartoon Network", "CBS Drama", "CDV", "Cinema Prestige",
                        "CinemaSET GROUP", "CinemaTone", "ColdFilm", "Contentica", "CP Digital", "CPIG", "Crunchyroll", "Cuba77", "D1", "D2lab", "datynet", "DDV", "DeadLine", "DeadSno", "DeMon", "den904", "Description",
                        "DexterTV", "Dice", "Discovery", "DniproFilm", "DoubleRec", "DreamRecords", "DVD Classic", "East Dream", "Eladiel", "Elegia", "ELEKTRI4KA", "Elrom", "ELYSIUM", "Epic Team", "eraserhead", "erogg",
                        "Eurochannel", "Extrabit", "F-TRAIN", "Family Fan Edition", "FDV", "FiliZa Studio", "Film Prestige", "FilmGate", "FilmsClub", "FireDub", "Flarrow Films", "Flux-Team", "FocusStudio", "FOX", "Fox Crime",
                        "Fox Russia", "FoxLife", "Foxlight", "Franek Monk", "Gala Voices", "Garsu Pasaulis", "Gears Media", "Gemini", "General Film", "GetSmart", "Gezell Studio", "Gits", "GladiolusTV", "GoldTeam", "Good People",
                        "Goodtime Media", "GoodVideo", "GostFilm", "Gramalant", "Gravi-TV", "GREEN TEA", "GreenРай Studio", "Gremlin Creative Studio", "Hallmark", "HamsterStudio", "HiWay Grope", "Horizon Studio", "hungry_inri",
                        "ICG", "ICTV", "IdeaFilm", "IgVin &amp; Solncekleshka", "ImageArt", "INTERFILM", "Ivnet Cinema", "IНТЕР", "Jakob Bellmann", "JAM", "Janetta", "Jaskier", "JeFerSon", "jept", "JetiX", "Jetvis", "JimmyJ",
                        "KANSAI", "KIHO", "kiitos", "KinoGolos", "Kinomania", "KosharaSerials", "Kолобок", "L0cDoG", "LakeFilms", "LDV", "LE-Production", "LeDoyen", "LevshaFilm", "LeXiKC", "Liga HQ", "Line", "Lisitz",
                        "Lizard Cinema Trade", "Lord32x", "lord666", "LostFilm", "Lucky Production", "Macross", "madrid", "Mallorn Studio", "Marclail", "Max Nabokov", "MC Entertainment", "MCA", "McElroy", "Mega-Anime",
                        "Melodic Voice Studio", "metalrus", "MGM", "MifSnaiper", "Mikail", "Milirina", "MiraiDub", "MOYGOLOS", "MrRose", "MTV", "Murzilka", "MUZOBOZ", "National Geographic", "NemFilm", "Neoclassica", "NEON Studio",
                        "New Dream Media", "NewComers", "NewStation", "NewStudio", "Nice-Media", "Nickelodeon", "No-Future", "NovaFilm", "Novamedia", "Octopus", "Oghra-Brown", "OMSKBIRD", "Onibaku", "OnisFilms", "OpenDub",
                        "OSLIKt", "Ozz TV", "PaDet", "Paramount Comedy", "Paramount Pictures", "Parovoz Production", "PashaUp", "Paul Bunyan", "Pazl Voice", "PCB Translate", "Persona99", "PiratVoice", "Postmodern", "Profix Media",
                        "Project Web Mania", "Prolix", "QTV", "R5", "Radamant", "RainDeath", "RATTLEBOX", "RealFake", "Reanimedia", "Rebel Voice", "RecentFilms", "Red Media", "RedDiamond Studio", "RedDog", "RedRussian1337",
                        "Renegade Team", "RG Paravozik", "RinGo", "RoxMarty", "Rumble", "RUSCICO", "RusFilm", "RussianGuy27", "Saint Sound", "SakuraNight", "Satkur", "Sawyer888", "Sci-Fi Russia", "SDI Media", "Selena", "seqw0",
                        "SesDizi", "SGEV", "Shachiburi", "SHIZA", "ShowJet", "Sky Voices", "SkyeFilmTV", "SmallFilm", "SmallFilm", "SNK-TV", "SnowRecords", "SOFTBOX", "SOLDLUCK2", "Solod", "SomeWax", "Sony Channel", "Sony Turbo",
                        "Sound Film", "SpaceDust", "ssvss", "st.Elrom", "STEPonee", "SunshineStudio", "Superbit", "Suzaku", "sweet couple", "TatamiFilm", "TB5", "TF-AniGroup", "The Kitchen Russia", "The Mike Rec.", "Timecraft",
                        "To4kaTV", "Tori", "Total DVD", "TrainStudio", "Troy", "True Dubbing Studio", "TUMBLER Studio", "turok1990", "TV 1000", "TVShows", "Twister", "Twix", "Tycoon", "Ultradox", "Universal Russia", "VashMax2",
                        "VendettA", "VHS", "VicTeam", "VictoryFilms", "Video-BIZ", "Videogram", "ViruseProject", "visanti-vasaer", "VIZ Media", "VO-production", "Voice Project Studio", "VoicePower", "VSI Moscow", "VulpesVulpes",
                        "Wakanim", "Wayland team", "WestFilm", "WiaDUB", "WVoice", "XL Media", "XvidClub Studio", "zamez", "ZEE TV", "Zendos", "ZM-SHOW", "Zone Studio", "Zone Vision", "Агапов", "Акопян", "Алексеев", "Артемьев",
                        "Багичев", "Бессонов", "Васильев", "Васильцев", "Гаврилов", "Герусов", "Готлиб", "Григорьев", "Дасевич", "Дольский", "Карповский", "Кашкин", "Киреев", "Клюквин", "Костюкевич", "Матвеев", "Михалев", "Мишин",
                        "Мудров", "Пронин", "Савченко", "Смирнов", "Тимофеев", "Толстобров", "Чуев", "Шуваев", "Яковлев", "ААА-sound", "АБыГДе", "Акалит", "Акира", "Альянс", "Амальгама", "АМС", "АнВад", "Анубис", "Anubis", "Арк-ТВ",
                        "АРК-ТВ Studio", "Б. Федоров", "Бибиков", "Бигыч", "Бойков", "Абдулов", "Белов", "Вихров", "Воронцов", "Горчаков", "Данилов", "Дохалов", "Котов", "Кошкин", "Назаров", "Попов", "Рукин", "Рутилов",
                        "Варус Видео", "Васька Куролесов", "Ващенко С.", "Векшин", "Велес", "Весельчак", "Видеоимпульс", "Витя «говорун»", "Войсовер", "Вольга", "Ворон", "Воротилин", "Г. Либергал", "Г. Румянцев", "Гей Кино Гид",
                        "ГКГ", "Глуховский", "Гризли", "Гундос", "Деньщиков", "Есарев", "Нурмухаметов", "Пучков", "Стасюк", "Шадинский", "Штамп", "sf@irat", "Держиморда", "Домашний", "ДТВ", "Дьяконов", "Е. Гаевский", "Е. Гранкин",
                        "Е. Лурье", "Е. Рудой", "Е. Хрусталёв", "ЕА Синема", "Екатеринбург Арт", "Живаго", "Жучков", "З Ранку До Ночі", "Завгородний", "Зебуро", "Зереницын", "И. Еремеев", "И. Клушин", "И. Сафронов", "И. Степанов",
                        "ИГМ", "Игмар", "ИДДК", "Имидж-Арт", "Инис", "Ирэн", "Ист-Вест", "К. Поздняков", "К. Филонов", "К9", "Карапетян", "Кармен Видео", "Карусель", "Квадрат Малевича", "Килька",  "Кипарис", "Королев", "Котова",
                        "Кравец", "Кубик в Кубе", "Кураж-Бамбей", "Л. Володарский", "Лазер Видео", "ЛанселаП", "Лапшин", "Лексикон", "Ленфильм", "Леша Прапорщик", "Лизард", "Люсьена", "Заугаров", "Иванов", "Иванова и П. Пашут",
                        "Латышев", "Ошурков", "Чадов", "Яроцкий", "Максим Логинофф", "Малиновский", "Марченко", "Мастер Тэйп", "Махонько", "Машинский", "Медиа-Комплекс", "Мельница", "Мика Бондарик", "Миняев", "Мительман",
                        "Мост Видео", "Мосфильм", "Муравский", "Мьюзик-трейд", "Н-Кино", "Н. Антонов", "Н. Дроздов", "Н. Золотухин", "Н.Севастьянов seva1988", "Набиев", "Наталья Гурзо", "НЕВА 1", "Невафильм", "НеЗупиняйПродакшн",
                        "Неоклассика", "Несмертельное оружие", "НЛО-TV", "Новий", "Новый диск", "Новый Дубляж", "Новый Канал", "Нота", "НСТ", "НТВ", "НТН", "Оверлорд", "Огородников", "Омикрон", "Гланц", "Карцев", "Морозов",
                        "Прямостанов", "Санаев", "Парадиз", "Пепелац", "Первый канал ОРТ", "Переводман", "Перец", "Петербургский дубляж", "Петербуржец", "Пирамида", "Пифагор", "Позитив-Мультимедиа", "Прайд Продакшн", "Премьер Видео",
                        "Премьер Мультимедиа", "Причудики", "Р. Янкелевич", "Райдо", "Ракурс", "РенТВ", "Россия", "РТР", "Русский дубляж", "Русский Репортаж", "РуФилмс", "Рыжий пес", "С. Визгунов", "С. Дьяков", "С. Казаков",
                        "С. Кузнецов", "С. Кузьмичёв", "С. Лебедев", "С. Макашов", "С. Рябов", "С. Щегольков", "С.Р.И.", "Сolumbia Service", "Самарский", "СВ Студия", "СВ-Дубль", "Светла", "Селена Интернешнл", "Синема Трейд",
                        "Синема УС", "Синта Рурони", "Синхрон", "Советский", "Сокуров", "Солодухин", "Сонотек", "Сонькин", "Союз Видео", "Союзмультфильм", "СПД - Сладкая парочка", "Строев", "СТС", "Студии Суверенного Лепрозория",
                        "Студия «Стартрек»", "KOleso", "Студия Горького", "Студия Колобок", "Студия Пиратского Дубляжа", "Студия Райдо", "Студия Трёх", "Гуртом", "Супербит", "Сыендук", "Так Треба Продакшн", "ТВ XXI век", "ТВ СПб",
                        "ТВ-3", "ТВ6", "ТВИН", "ТВЦ", "ТВЧ 1", "ТНТ", "ТО Друзей", "Толмачев", "Точка Zрения", "Трамвай-фильм", "ТРК", "Уолт Дисней Компани", "Хихидок", "Хлопушка", "Цікава Ідея", "Четыре в квадрате", "Швецов",
                        "Штамп", "Штейн", "Ю. Живов", "Ю. Немахов", "Ю. Сербин", "Ю. Товбин", "Я. Беллманн", "Украинский"
                    };

                    foreach (string v in allVoices)
                    {
                        if (v.Length > 4 && t.title.ToLower().Contains(v.ToLower()))
                            t.voices.Add(v);
                    }
                }
                #endregion

                #region seasons
                t.seasons = new HashSet<int>();

                if (t.types.Contains("serial") || t.types.Contains("multserial") || t.types.Contains("docuserial") || t.types.Contains("tvshow") || t.types.Contains("anime"))
                {
                    if (Regex.IsMatch(t.title, "([0-9]+(\\-[0-9]+)?x[0-9]+|сезон|s[0-9]+)", RegexOptions.IgnoreCase))
                    {
                        if (Regex.IsMatch(t.title, "([0-9]+\\-[0-9]+x[0-9]+|[0-9]+\\-[0-9]+ сезон|s[0-9]+\\-[0-9]+)", RegexOptions.IgnoreCase))
                        {
                            #region Несколько сезонов
                            int startSeason = 0, endSeason = 0;

                            if (Regex.IsMatch(t.title, "[0-9]+x[0-9]+", RegexOptions.IgnoreCase))
                            {
                                var g = Regex.Match(t.title, "([0-9]+)\\-([0-9]+)x", RegexOptions.IgnoreCase).Groups;
                                int.TryParse(g[1].Value, out startSeason);
                                int.TryParse(g[2].Value, out endSeason);
                            }
                            else if (Regex.IsMatch(t.title, "[0-9]+ сезон", RegexOptions.IgnoreCase))
                            {
                                var g = Regex.Match(t.title, "([0-9]+)\\-([0-9]+) сезон", RegexOptions.IgnoreCase).Groups;
                                int.TryParse(g[1].Value, out startSeason);
                                int.TryParse(g[2].Value, out endSeason);
                            }
                            else if (Regex.IsMatch(t.title, "s[0-9]+", RegexOptions.IgnoreCase))
                            {
                                var g = Regex.Match(t.title, "s([0-9]+)\\-([0-9]+)", RegexOptions.IgnoreCase).Groups;
                                int.TryParse(g[1].Value, out startSeason);
                                int.TryParse(g[2].Value, out endSeason);
                            }

                            if (startSeason > 0 && endSeason > startSeason)
                            {
                                for (int s = startSeason; s <= endSeason; s++)
                                    t.seasons.Add(s);
                            }
                            #endregion
                        }
                        if (Regex.IsMatch(t.title, "[0-9]+ сезон", RegexOptions.IgnoreCase))
                        {
                            #region Один сезон
                            if (Regex.IsMatch(t.title, "[0-9]+ сезон", RegexOptions.IgnoreCase))
                            {
                                if (int.TryParse(Regex.Match(t.title, "([0-9]+) сезон", RegexOptions.IgnoreCase).Groups[1].Value, out int s) && s > 0)
                                    t.seasons.Add(s);
                            }
                            #endregion
                        }
                        else if (Regex.IsMatch(t.title, "сезон(ы|и)?:? [0-9]+\\-[0-9]+", RegexOptions.IgnoreCase))
                        {
                            #region Несколько сезонов
                            int startSeason = 0, endSeason = 0;

                            if (Regex.IsMatch(t.title, "сезон(ы|и)?:? [0-9]+", RegexOptions.IgnoreCase))
                            {
                                var g = Regex.Match(t.title, "сезон(ы|и)?:? ([0-9]+)\\-([0-9]+)", RegexOptions.IgnoreCase).Groups;
                                int.TryParse(g[2].Value, out startSeason);
                                int.TryParse(g[3].Value, out endSeason);
                            }

                            if (startSeason > 0 && endSeason > startSeason)
                            {
                                for (int s = startSeason; s <= endSeason; s++)
                                    t.seasons.Add(s);
                            }
                            #endregion
                        }
                        else
                        {
                            #region Один сезон
                            if (Regex.IsMatch(t.title, "[0-9]+x[0-9]+", RegexOptions.IgnoreCase))
                            {
                                if (int.TryParse(Regex.Match(t.title, "([0-9]+)x", RegexOptions.IgnoreCase).Groups[1].Value, out int s) && s > 0)
                                    t.seasons.Add(s);
                            }
                            else if (Regex.IsMatch(t.title, "сезон(ы|и)?:? [0-9]+", RegexOptions.IgnoreCase))
                            {
                                if (int.TryParse(Regex.Match(t.title, "сезон(ы|и)?:? ([0-9]+)", RegexOptions.IgnoreCase).Groups[2].Value, out int s) && s > 0)
                                    t.seasons.Add(s);
                            }
                            else if (Regex.IsMatch(t.title, "s[0-9]+", RegexOptions.IgnoreCase))
                            {
                                if (int.TryParse(Regex.Match(t.title, "s([0-9]+)", RegexOptions.IgnoreCase).Groups[1].Value, out int s) && s > 0)
                                    t.seasons.Add(s);
                            }
                            #endregion
                        }
                    }
                }
                #endregion
            }

            end: return torrents.ToList();
        }
        #endregion
    }
}

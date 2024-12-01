﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Web;
using System.Linq;
using System.Collections.Generic;
using Lampac.Engine.CORE;
using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using Online;
using Shared.Model.Online.PiTor;
using Shared.Model.Templates;
using Shared.Model.Online;
using System.Data;
using System.IO;
using Shared.Model.Online.Settings;
using Shared.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class PiTor : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/pidtor")]
        async public Task<ActionResult> Index(string title, string original_title, int year, string original_language, int serial, int s = -1, bool rjson = false)
        {
            var init = AppInit.conf.PidTor;
            if (!init.enable)
                return OnError();

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            #region Кеш запроса
            string memKey = $"pidtor:{title}:{original_title}:{year}";
            if (!memoryCache.TryGetValue(memKey, out List<(string name, string voice, string magnet, int sid, string tr, string quality, long size, string mediainfo, Result torrent)> torrents))
            {
                var root = await HttpClient.Get<RootObject>($"{init.redapi}/api/v2.0/indexers/all/results?title={HttpUtility.UrlEncode(title)}&title_original={HttpUtility.UrlEncode(original_title)}&year={year}&is_serial={(original_language == "ja" ? 5 : (serial + 1))}&apikey={init.apikey}", timeoutSeconds: 8);
                if (root == null)
                    return Content(string.Empty, "text/html; charset=utf-8");

                torrents = new List<(string name, string voice, string magnet, int sid, string tr, string quality, long size, string mediainfo, Result torrent)>();
                var results = root?.Results;
                if (results != null && results.Count > 0)
                {
                    foreach (var torrent in results)
                    {
                        string magnet = torrent.MagnetUri;
                        string name = torrent.Title;

                        if (string.IsNullOrWhiteSpace(magnet) || !magnet.Contains("&tr=") || string.IsNullOrWhiteSpace(name))
                            continue;

                        string tracker = torrent.Tracker;
                        if (tracker == "selezen")
                            continue;

                        if (init.max_serial_size > 0 && init.max_size > 0)
                        {
                            if (serial == 1)
                            {
                                if (torrent.Size > init.max_serial_size)
                                    continue;
                            }
                            else
                            {
                                if (torrent.Size > init.max_size)
                                    continue;
                            }
                        }
                        else
                        {
                            if (init.max_size > 0 && torrent.Size > init.max_size)
                                continue;
                        }

                        if (Regex.IsMatch(name.ToLower(), "(4k|uhd)( |\\]|,|$)") || name.Contains("2160p") || name.Contains("1080p") || name.Contains("720p"))
                        {
                            int sid = torrent.Seeders;
                            long? size = torrent.Size;

                            if (sid >= init.min_sid)
                            {
                                string mediainfo = torrent.info?.sizeName ?? string.Empty;
                                if (!string.IsNullOrEmpty(mediainfo))
                                    mediainfo += " / ";

                                #region Перевод
                                string voicename = string.Empty;

                                var voices = torrent.info?.voices;
                                if (voices != null && voices.Count > 0)
                                    voicename = string.Join(", ", voices);
                                #endregion

                                #region Перевод 2
                                if (string.IsNullOrWhiteSpace(voicename))
                                {
                                    if (Regex.IsMatch(name.ToLower(), "( дб| d|дубляж)", RegexOptions.IgnoreCase))
                                        voicename += "Дубляж, ";

                                    if (Regex.IsMatch(name.ToLower(), "( ст| пм)", RegexOptions.IgnoreCase))
                                        voicename += "Многоголосый, ";

                                    if (torrent.Tracker.ToLower() == "lostfilm")
                                    {
                                        voicename += "LostFilm, ";
                                    }
                                    else if (torrent.Tracker.ToLower() == "toloka")
                                    {
                                        voicename += "Украинский, ";
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
                                            if (v.Length > 4 && name.ToLower().Contains(v.ToLower()))
                                                voicename += $"{v}, ";
                                        }
                                    }

                                    voicename = Regex.Replace(voicename, ", +$", "");
                                }
                                #endregion

                                if (string.IsNullOrWhiteSpace(voicename))
                                    continue;

                                #region HDR / HEVC / Dolby Vision
                                if (Regex.IsMatch(name, "HDR10", RegexOptions.IgnoreCase) || Regex.IsMatch(name, "10-?bit", RegexOptions.IgnoreCase))
                                    mediainfo += " HDR10 ";
                                else if (Regex.IsMatch(name, "HDR", RegexOptions.IgnoreCase))
                                    mediainfo += " HDR ";
                                else
                                {
                                    mediainfo += " SDR ";
                                }

                                if (Regex.IsMatch(name, "HEVC", RegexOptions.IgnoreCase) || Regex.IsMatch(name, "H.265", RegexOptions.IgnoreCase))
                                    mediainfo += " / H.265 ";

                                if (Regex.IsMatch(name, "Dolby Vision", RegexOptions.IgnoreCase))
                                    mediainfo += " / Dolby Vision ";
                                #endregion

                                #region tr arg
                                string tr = string.Empty;
                                var match = Regex.Match(magnet, "(&|\\?)tr=([^&\\?]+)");
                                while (match.Success)
                                {
                                    string t = match.Groups[2].Value.Trim().ToLower();
                                    if (!string.IsNullOrWhiteSpace(t))
                                        tr += t.Contains("/") || t.Contains(":") ? $"&tr={HttpUtility.UrlEncode(t)}" : $"&tr={t}";

                                    match = match.NextMatch();
                                }

                                if (string.IsNullOrWhiteSpace(tr))
                                    continue;
                                #endregion

                                if (!string.IsNullOrEmpty(init.filter) && !Regex.IsMatch($"{name}:{voicename}", init.filter, RegexOptions.IgnoreCase))
                                    continue;

                                if (!string.IsNullOrEmpty(init.filter_ignore) && Regex.IsMatch($"{name}:{voicename}", init.filter_ignore, RegexOptions.IgnoreCase))
                                    continue;

                                torrents.Add((name, voicename, magnet, sid, tr.Remove(0, 1), (name.Contains("2160p") ? "2160p" : "1080p"), (torrent.Size ?? 0), mediainfo, torrent));
                            }
                        }
                    }
                }

                memoryCache.Set(memKey, torrents, DateTime.Now.AddMinutes(5));
            }

            if (torrents.Count == 0)
                return Content(string.Empty);
            #endregion
             
            string en_title = HttpUtility.UrlEncode(title);
            string en_original_title = HttpUtility.UrlEncode(original_title);

            var movies = torrents.OrderByDescending(i => i.voice.Contains("Дубляж")).ThenByDescending(i => !string.IsNullOrWhiteSpace(i.voice));
            movies = init.sort == "size" ? movies.ThenByDescending(i => i.size) : init.sort == "sid" ? movies.ThenByDescending(i => i.sid) : movies.ThenByDescending(i => i.torrent.PublishDate);

            if (serial == 1)
            {
                if (s == -1)
                {
                    HashSet<int> seasons = new HashSet<int>();

                    var tpl = new SeasonTpl(quality: movies.FirstOrDefault
                    (
                        i => Regex.IsMatch(i.name, "(4k|uhd)( |\\]|,|$)", RegexOptions.IgnoreCase) || i.name.Contains("2160p")).name != null ? "2160p" :
                             movies.FirstOrDefault(i => i.name.Contains("1080p")).name != null ? "1080p" : "720p"
                    ); 

                    foreach (var t in movies)
                    {
                        if (t.torrent?.info?.seasons == null || t.torrent.info.seasons.Count == 0)
                            continue;

                        foreach (var item in t.torrent.info.seasons)
                            seasons.Add(item);
                    }

                    foreach (int season in seasons.OrderBy(i => i))
                        tpl.Append($"{season} сезон", $"{host}/lite/pidtor?rjson={rjson}&title={en_title}&original_title={en_original_title}&year={year}&original_language={original_language}&serial=1&s={season}", season);

                    return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                }
                else
                {
                    var stpl = new SimilarTpl();

                    foreach (var torrent in movies)
                    {
                        if (torrent.torrent?.info?.seasons == null || torrent.torrent.info.seasons.Count == 0)
                            continue;

                        if (!torrent.torrent.info.seasons.Contains(s) || torrent.torrent.info.seasons.Count != 1) // многосезонный 
                            continue;

                        string hashmagnet = Regex.Match(torrent.magnet, "magnet:\\?xt=urn:btih:([a-zA-Z0-9]+)").Groups[1].Value.ToLower();
                        if (string.IsNullOrWhiteSpace(hashmagnet))
                            continue;

                        stpl.Append(torrent.voice, null, $"{torrent.quality} / {torrent.mediainfo} / {torrent.sid}", $"{host}/lite/pidtor/serial/{hashmagnet}?{torrent.tr}&rjson={rjson}&title={en_title}&original_title={en_original_title}&s={s}");
                    }

                    return ContentTo(rjson ? stpl.ToJson() : stpl.ToHtml());
                }
            }
            else
            {
                var mtpl = new MovieTpl(title, original_title);

                foreach (var torrent in movies)
                {
                    string hashmagnet = Regex.Match(torrent.magnet, "magnet:\\?xt=urn:btih:([a-zA-Z0-9]+)").Groups[1].Value.ToLower();
                    if (string.IsNullOrWhiteSpace(hashmagnet))
                        continue;

                    mtpl.Append(torrent.voice, accsArgs($"{host}/lite/pidtor/s{hashmagnet}?{torrent.tr}"), voice_name: $"{torrent.quality} / {torrent.mediainfo} / {torrent.sid}", quality: torrent.quality.Replace("p", ""));
                }

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
            }
        }


        [HttpGet]
        [Route("lite/pidtor/serial/{id}")]
        async public Task<ActionResult> Serial(string id, string title, string original_title, int s, bool rjson = false)
        {
            var init = AppInit.conf.PidTor;
            if (!init.enable)
                return OnError();

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            string tr = Regex.Replace(HttpContext.Request.QueryString.Value.Remove(0, 1), "&(title|account_email|original_title)=[^&]+", "");
            string magnet = $"magnet:?xt=urn:btih:{id}&" + tr;

            (List<HeadersModel> header, string host) gots()
            {
                if ((init.torrs == null || init.torrs.Length == 0) && (init.auth_torrs == null || init.auth_torrs.Count == 0))
                {
                    if (System.IO.File.Exists("torrserver/accs.db"))
                    {
                        string accs = System.IO.File.ReadAllText("torrserver/accs.db");
                        string passwd = Regex.Match(accs, "\"ts\":\"([^\"]+)\"").Groups[1].Value;

                        return (HeadersModel.Init("Authorization", $"Basic {CrypTo.Base64($"ts:{passwd}")}"), $"http://{AppInit.conf.localhost}:9080");
                    }

                    return (null, $"http://{AppInit.conf.localhost}:9080");
                }

                if (init.auth_torrs != null && init.auth_torrs.Count > 0)
                {
                    var ts = init.auth_torrs.First();
                    string login = ts.login.Replace("{account_email}", accsArgs(string.Empty));

                    return (HeadersModel.Init("Authorization", $"Basic {CrypTo.Base64($"{login}:{ts.passwd}")}"), ts.host);
                }
                else
                {
                    if (init.base_auth != null && init.base_auth.enable)
                    {
                        var ts = init.auth_torrs.First();
                        string login = init.base_auth.login.Replace("{account_email}", accsArgs(string.Empty));

                        return (HeadersModel.Init("Authorization", $"Basic {CrypTo.Base64($"{login}:{init.base_auth.passwd}")}"), ts.host);
                    }

                    return (null, init.torrs.First());
                }
            }


            string tskey = $"pidtor:ts:{id}:{requestInfo.IP}";
            if (!memoryCache.TryGetValue(tskey, out (List<HeadersModel> header, string host) ts))
            {
                ts = gots();
                memoryCache.Set(tskey, ts, DateTime.Now.AddHours(4));
            }

            string hash = await HttpClient.Post($"{ts.host}/torrents", "{\"action\":\"add\",\"link\":\"" + magnet + "\",\"title\":\"\",\"poster\":\"\",\"save_to_db\":false}", timeoutSeconds: 8, headers: ts.header);
            if (hash == null)
                return OnError();

            hash = Regex.Match(hash, "\"hash\":\"([^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrEmpty(hash))
                return OnError();

            Stat stat = null;
            DateTime startGotInfoTime = DateTime.Now;

            resetgotingo: stat = await HttpClient.Post<Stat>($"{ts.host}/torrents", "{\"action\":\"get\",\"hash\":\"" + hash + "\"}", timeoutSeconds: 3, headers: ts.header);
            if (stat?.file_stats == null || stat.file_stats.Count == 0)
            {
                if (DateTime.Now > startGotInfoTime.AddSeconds(20))
                {
                    _ = await HttpClient.Post($"{ts.host}/torrents", "{\"action\":\"rem\",\"hash\":\"" + hash + "\"}", timeoutSeconds: 2, headers: ts.header);
                    return OnError();
                }

                await Task.Delay(250);
                goto resetgotingo;
            }


            var mtpl = new EpisodeTpl();

            foreach (var torrent in stat.file_stats)
            {
                if (Path.GetExtension(torrent.Path) is ".srt" or ".txt" or ".jpg" or ".png")
                    continue;

                mtpl.Append(Path.GetFileName(torrent.Path), title ?? original_title, s.ToString(), torrent.Id.ToString(), accsArgs($"{host}/lite/pidtor/s{id}?{tr}&tsid={torrent.Id}"));
            }

            return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
        }


        [HttpGet]
        [Route("lite/pidtor/s{id}")]
        async public Task<ActionResult> Stream(string id, int tsid = -1, string account_email = null)
        {
            var init = AppInit.conf.PidTor;
            if (!init.enable)
                return OnError();

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            string country = requestInfo.Country;

            int index = tsid != -1 ? tsid : 1;
            string magnet = $"magnet:?xt=urn:btih:{id}&" + Regex.Replace(HttpContext.Request.QueryString.Value.Remove(0, 1), "&(account_email|tsid)=[^&]+", "");

            #region auth_stream
            async ValueTask<RedirectResult> auth_stream(string host, string login, string passwd, string uhost = null)
            {
                login = login.Replace("{account_email}", account_email ?? string.Empty);

                var headers = HeadersModel.Init("Authorization", $"Basic {CrypTo.Base64($"{login}:{passwd}")}");
                await HttpClient.Post($"{host}/torrents", "{\"action\":\"add\",\"link\":\"" + magnet + "\",\"title\":\"\",\"poster\":\"\",\"save_to_db\":false}", timeoutSeconds: 5, headers: headers);

                return Redirect($"{uhost ?? host}/stream?link={HttpUtility.UrlEncode($"magnet:?xt=urn:btih:{id}")}&index={index}&play");
            }
            #endregion

            if ((init.torrs == null || init.torrs.Length == 0) && (init.auth_torrs == null || init.auth_torrs.Count == 0))
            {
                if (System.IO.File.Exists("torrserver/accs.db"))
                {
                    string accs = System.IO.File.ReadAllText("torrserver/accs.db");
                    string passwd = Regex.Match(accs, "\"ts\":\"([^\"]+)\"").Groups[1].Value;

                    return await auth_stream($"http://{AppInit.conf.localhost}:9080", "ts", passwd, uhost: $"{host}/ts");
                }

                return Redirect($"{host}/ts/stream?link={HttpUtility.UrlEncode(magnet)}&index={index}&play");
            }

            if (init.auth_torrs != null && init.auth_torrs.Count > 0)
            {
                string tskey = $"pidtor:ts2:{id}:{requestInfo.IP}";
                if (!memoryCache.TryGetValue(tskey, out PidTorAuthTS ts))
                {
                    var tors = init.auth_torrs.Where(i => i.enable).ToList();

                    if (country != null)
                        tors = tors.Where(i => i.country == null || i.country.Contains(country)).Where(i => i.no_country == null || !i.no_country.Contains(country)).ToList();

                    ts = tors[Random.Shared.Next(0, tors.Count)];
                    memoryCache.Set(tskey, ts, DateTime.Now.AddHours(4));
                }

                return await auth_stream(ts.host, ts.login, ts.passwd);
            }
            else 
            {
                if (init.base_auth != null && init.base_auth.enable)
                {
                    string tskey = $"pidtor:ts3:{id}:{requestInfo.IP}";
                    if (!memoryCache.TryGetValue(tskey, out string ts))
                    {
                        ts = init.torrs[Random.Shared.Next(0, init.torrs.Length)];
                        memoryCache.Set(tskey, ts, DateTime.Now.AddHours(4));
                    }

                    return await auth_stream(ts, init.base_auth.login, init.base_auth.passwd);
                }

                string key = $"pidtor:ts4:{id}:{requestInfo.IP}";
                if (!memoryCache.TryGetValue(key, out string tshost))
                {
                    tshost = init.torrs[Random.Shared.Next(0, init.torrs.Length)];
                    memoryCache.Set(key, tshost, DateTime.Now.AddHours(4));
                }

                return Redirect($"{tshost}/stream?link={HttpUtility.UrlEncode(magnet)}&index={index}&play");
            }
        }
    }
}

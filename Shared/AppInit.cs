using Lampac.Engine.CORE;
using Lampac.Models.AppConf;
using Lampac.Models.DLNA;
using Lampac.Models.LITE;
using Lampac.Models.Merchant;
using Lampac.Models.Module;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Shared.Model.Base;
using Shared.Model.Online;
using Shared.Model.Online.Settings;
using Shared.Model.SISI;
using Shared.Models.AppConf;
using Shared.Models.Browser;
using Shared.Models.Online.Kinobase;
using Shared.Models.ServerProxy;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace Lampac
{
    public class AppInit
    {
        #region static
        public static string rootPasswd;

        public static bool Win32NT => Environment.OSVersion.Platform == PlatformID.Win32NT;

        public static bool IsDefaultApnOrCors(string apn) => apn != null && Regex.IsMatch(apn, "(apn.monster|apn.watch|cfhttp.top|lampac.workers.dev)");

        static FileSystemWatcher fileWatcher;

        static AppInit()
        {
            updateConf();
            LoadModules();

            #region watcherInit
            if (conf.watcherInit == "system")
            {
                fileWatcher = new FileSystemWatcher
                {
                    Path = Directory.GetCurrentDirectory(),
                    Filter = "init.conf",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };

                fileWatcher.Changed += async (s, e) =>
                {
                    fileWatcher.EnableRaisingEvents = false;

                    try
                    {
                        await Task.Delay(200);
                        updateConf();
                    }
                    finally
                    {
                        fileWatcher.EnableRaisingEvents = true;
                    }
                };
            }
            else
            {
                ThreadPool.QueueUserWorkItem(async _ =>
                {
                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

                        try
                        {
                            if (File.Exists("init.conf"))
                            {
                                var lwt = File.GetLastWriteTime("init.conf");
                                if (lwt != lastUpdateConf)
                                {
                                    updateConf();
                                    lastUpdateConf = lwt;
                                }
                            }
                        }
                        catch { }
                    }
                });
            }
            #endregion
        }
        #endregion


        #region conf
        static DateTime lastUpdateConf = default;

        public static AppInit conf = null;

        static void updateConf()
        {
            if (!File.Exists("init.conf"))
            {
                conf = new AppInit();
                return;
            }

            string initfile = File.ReadAllText("init.conf").Trim();
            initfile = Regex.Replace(initfile, "\"weblog\":([ \t]+)?(true|false)([ \t]+)?,", "", RegexOptions.IgnoreCase);

            if (!initfile.StartsWith("{"))
                initfile = "{" + initfile + "}";

            try
            {
                conf = JsonConvert.DeserializeObject<AppInit>(initfile, new JsonSerializerSettings
                {
                    Error = (se, ev) =>
                    {
                        ev.ErrorContext.Handled = true;
                        Console.WriteLine($"DeserializeObject Exception init.conf:\n{ev.ErrorContext.Error}\n\n");
                    }
                });
            }
            catch { }

            if (conf == null)
                conf = new AppInit();

            PosterApi.Initialization(conf.omdbapi_key, conf.posterApi, new ProxyLink());

            #region accounts
            if (conf.accsdb.accounts != null)
            {
                foreach (var u in conf.accsdb.accounts)
                {
                    if (conf.accsdb.findUser(u.Key) is AccsUser user)
                    {
                        if (u.Value > user.expires)
                            user.expires = u.Value;
                    }
                    else
                    {
                        conf.accsdb.users.Add(new AccsUser()
                        {
                            id = u.Key.ToLower().Trim(),
                            expires = u.Value
                        });
                    }
                }
            }
            #endregion

            #region users.txt
            if (File.Exists("merchant/users.txt"))
            {
                long utc = DateTime.UtcNow.ToFileTimeUtc();
                foreach (string line in File.ReadAllLines("merchant/users.txt"))
                {
                    if (string.IsNullOrWhiteSpace(line) && !line.Contains("@"))
                        continue;

                    var data = line.Split(',');
                    if (data.Length > 1)
                    {
                        if (long.TryParse(data[1], out long ex) && ex > utc)
                        {
                            try
                            {
                                DateTime e = DateTime.FromFileTimeUtc(ex);
                                string email = data[0].ToLower().Trim();

                                if (conf.accsdb.findUser(email) is AccsUser user)
                                {
                                    if (e > user.expires)
                                        user.expires = e;

                                    user.group = conf.Merchant.defaultGroup;
                                }
                                else
                                {
                                    conf.accsdb.users.Add(new AccsUser()
                                    {
                                        id = email,
                                        expires = e,
                                        group = conf.Merchant.defaultGroup
                                    });
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            #endregion

            try
            {
                File.WriteAllText("current.conf", JsonConvert.SerializeObject(conf, Formatting.Indented));
            }
            catch { }
        }
        #endregion

        #region Host
        public static string Host(HttpContext httpContext)
        {
            string scheme = string.IsNullOrEmpty(conf.listenscheme) ? httpContext.Request.Scheme : conf.listenscheme;
            if (httpContext.Request.Headers.TryGetValue("xscheme", out var xscheme) && !string.IsNullOrEmpty(xscheme))
                scheme = xscheme;

            if (!string.IsNullOrEmpty(conf.listenhost))
                return $"{scheme}://{conf.listenhost}";

            if (httpContext.Request.Headers.TryGetValue("xhost", out var xhost))
                return $"{scheme}://{Regex.Replace(xhost, "^https?://", "")}";

            return $"{scheme}://{httpContext.Request.Host.Value}";
        }
        #endregion

        #region modules
        public static List<RootModule> modules;

        public static void LoadModules()
        {
            modules = null;

            if (File.Exists("module/manifest.json"))
            {
                var jss = new JsonSerializerSettings { Error = (se, ev) => 
                { 
                    ev.ErrorContext.Handled = true; 
                    Console.WriteLine("module/manifest.json - " + ev.ErrorContext.Error + "\n\n"); 
                }};

                var mods = JsonConvert.DeserializeObject<List<RootModule>>(File.ReadAllText("module/manifest.json"), jss);
                if (mods == null)
                    return;

                modules = new List<RootModule>();
                foreach (var mod in mods)
                {
                    if (!mod.enable || mod.dll == "Jackett.dll")
                        continue;

                    string path = File.Exists(mod.dll) ? mod.dll : $"{Environment.CurrentDirectory}/module/{mod.dll}";
                    if (File.Exists(path))
                    {
                        try
                        {
                            mod.assembly = Assembly.LoadFile(path);
                            mod.index = mod.index != 0 ? mod.index : (100 + modules.Count);
                            modules.Add(mod);
                        }
                        catch { }
                    }
                }
            }
        }
        #endregion

        
        #region server
        public string listenip = "any";

        public int listenport = 9118;

        public bool compression = true;

        public string listen_sock = null;

        public string listenscheme = null;

        public string listenhost = null;

        public string frontend = null; // cloudflare|nginx

        public string localhost = "127.0.0.1";

        public bool multiaccess = false;

        public bool mikrotik = false;

        public string typecache = "hybrid"; // mem|file|hybrid

        public int cacheHybridExtend = 5; // seconds

        public string watcherInit = "cron";// system

        public string imagelibrary = "NetVips"; // NetVips|ImageMagick|none

        public bool pirate_store = true;

        public string apikey = null;

        public bool litejac = true;

        public bool filelog = false;

        public bool disableEng = false;

        public string anticaptchakey;

        public string omdbapi_key;

        public string playerInner;

        public string defaultOn = "enable";

        public string corsehost { get; set; } = "https://cors.apn.monster";

        public ApnConf apn { get; set; } = new ApnConf() { host = "http://apn.cfhttp.top", secure = "none" };

        public Dictionary<string, CmdConf> cmd = new Dictionary<string, CmdConf>();

        public PosterApiConf posterApi = new PosterApiConf() 
        {
            rsize = true, width = 210,
            bypass = "statichdrezka\\."
        };

        public KitConf kit = new KitConf() { cacheToSeconds = 20 };

        public SyncConf sync = new SyncConf();

        public WafConf WAF = new WafConf();

        public WebLogConf weblog = new WebLogConf();

        public OpenStatConf openstat = new OpenStatConf();

        public RchConf rch = new RchConf() { enable = true, keepalive = 45, permanent_connection = true };

        public StorageConf storage = new StorageConf() { enable = true, max_size = 7_000000, brotli = false, md5name = true };

        public PuppeteerConf chromium = new PuppeteerConf() 
        { 
            enable = true, Xvfb = true,
            Args = new string[] { "--disable-blink-features=AutomationControlled", "--window-position=-2000,100" },
            context = new KeepopenContext() { keepopen = true, keepalive = 20, min = 0, max = 4 }
        };

        public PuppeteerConf firefox = new PuppeteerConf()
        {
            enable = false, Headless = true,
            Args = new string[] { "--window-position=-2000,100" },
            context = new KeepopenContext() { keepopen = true, keepalive = 20, min = 1, max = 2 }
        };

        public FfprobeSettings ffprobe = new FfprobeSettings() { enable = true };

        public CubConf cub { get; set; } = new CubConf()
        {
            enable = false, viewru = true,
            domain = CrypTo.DecodeBase64("Y3ViLnJlZA=="), scheme = "http",
            mirror = "mirror-kurwa.men",
            cache_api = 20, cache_img = 120,
        };

        public TmdbConf tmdb { get; set; } = new TmdbConf()
        {
            enable = true,
            httpversion = 2,
            DNS = "9.9.9.9", DNS_TTL = 20,
            cache_api = 20, cache_img = 60, check_img = false,
            api_key = "4ef0d7355d9ffb5151e987764708ce96"
        };

        public ServerproxyConf serverproxy = new ServerproxyConf()
        {
            enable = true, encrypt = true, verifyip = true,
            buffering = new ServerproxyBufferingConf()
            {
                enable = true, 
                rent = 81920, // 80KB
                length = 390  // 30MB
            },
            image = new ServerproxyImageConf()
            {
                cache = true, 
                cache_rsize = true,
                cache_time = 60 // minute
            },
            maxlength_m3u = 5000000, // 5mb
            maxlength_ts = 40000000  // 40mb
        };

        public FileCacheConf fileCacheInactive = new FileCacheConf() 
        { 
            hls = 90, html = 5, torrent = 2880 // minute
        };

        public DLNASettings dlna = new DLNASettings() 
        { 
            enable = true, path = "dlna",
            uploadSpeed = 125000 * 10,
            autoupdatetrackers = true, intervalUpdateTrackers = 90, addTrackersToMagnet = true, 
            cover = new CoverSettings() 
            {
                enable = true, preview = true, 
                timeout = 20, skipModificationTime = 60,
                priorityClass = System.Diagnostics.ProcessPriorityClass.Idle,
                extension = "(mp4|mkv|avi|mpg|mpe|mpv)",
                coverComand = "-n -ss 3:00 -i \"{file}\" -vf \"thumbnail=150,scale=400:-2\" -frames:v 1 \"{thumb}\"",
                // фрэмрэйт -r   : больше - больше файл , плавнее движения
                // качество -crf : больше - меньше файл , появляются артефакты
                previewComand = "-n -ss 00:03:00 -to 00:03:07 -i \"{file}\" -ss 00:04:37 -to 00:04:44 -i \"{file}\" -ss 00:06:41 -to 00:06:48 -i \"{file}\" -ss 00:09:20 -to 00:09:27 -i \"{file}\" -ss 00:12:45 -to 00:12:52 -i \"{file}\" -ss 00:17:09 -to 00:17:16 -i \"{file}\" -ss 00:22:50 -to 00:22:57 -i \"{file}\" -ss 00:29:51 -to 00:29:58 -i \"{file}\" -ss 00:39:23 -to 00:39:30 -i \"{file}\" -ss 00:51:44 -to 00:51:51 -i \"{file}\" -ss 01:07:45 -to 01:07:52 -i \"{file}\" -ss 01:20:33 -to 01:20:40 -i \"{file}\" -filter_complex \"[0:v]setpts=0.5*PTS,scale=-2:240[v0];[1:v]setpts=0.5*PTS,scale=-2:240[v1];[2:v]setpts=0.5*PTS,scale=-2:240[v2];[3:v]setpts=0.5*PTS,scale=-2:240[v3];[4:v]setpts=0.5*PTS,scale=-2:240[v4];[5:v]setpts=0.5*PTS,scale=-2:240[v5];[6:v]setpts=0.5*PTS,scale=-2:240[v6];[7:v]setpts=0.5*PTS,scale=-2:240[v7];[8:v]setpts=0.5*PTS,scale=-2:240[v8];[9:v]setpts=0.5*PTS,scale=-2:240[v9];[10:v]setpts=0.5*PTS,scale=-2:240[v10];[11:v]setpts=0.5*PTS,scale=-2:240[v11];[v0][v1][v2][v3][v4][v5][v6][v7][v8][v9][v10][v11]concat=n=12:v=1:a=0\" -an -r 20 -c:v libx264 -crf 28 -preset veryslow \"{preview}\""
            }
        };

        public WebConf LampaWeb = new WebConf()
        {
            autoupdate = true,
            intervalupdate = 90, // minute
            basetag = true, index = "lampa-main/index.html",
            tree = "4e69d324e3e5e8c0a4379b0f89a7ce6bd96173cf"
        };

        public OnlineConf online = new OnlineConf()
        {
            findkp = "all", checkOnlineSearch = true, 
            spider = true, spiderName = "Spider",
            component = "lampac", name = "Lampac", description = "Плагин для просмотра онлайн сериалов и фильмов",
            version = true, btn_priority_forced = true, showquality = true,
            with_search = new List<string>() { "kinotochka", "kinobase", "kinopub", "lumex", "filmix", "filmixtv", "fxapi", "redheadsound", "animevost", "animego", "animedia", "animebesst", "anilibria", "aniliberty", "rezka", "rhsprem", "kodik", "remux", "animelib", "kinoukr", "rc/filmix", "rc/fxapi", "rc/rhs", "vcdn", "videocdn", "lumex", "collaps", "collaps-dash", "vdbmovies", "hdvb", "mirage", "alloha", "veoveo", "rutubemovie" }
        };

        public SisiConf sisi { get; set; } = new SisiConf()
        {
            NextHUB = true, spider = true,
            component = "sisi", iconame = "", push_all = true,
            heightPicture = 240, rsize = true, rsize_disable = new string[] { "BongaCams", "Chaturbate", "PornHub", "PornHubPremium", "HQporner", "Spankbang", "Porntrex", "Xnxx" },
            bookmarks = new BookmarksConf() { saveimage = true, savepreview = true }
        };

        public AccsConf accsdb = new AccsConf() 
        { 
            authMesage = "Войдите в аккаунт - настройки, синхронизация",
            denyMesage = "Добавьте {account_email} в init.conf",
            denyGroupMesage = "У вас нет прав для просмотра этой страницы",
            expiresMesage = "Время доступа для {account_email} истекло в {expires}",
            maxip_hour = 15, maxrequest_hour = 300, maxlock_day = 3, blocked_hour = 36 
        };

        public MerchantsModel Merchant = new MerchantsModel();

        public VastConf vast = new VastConf();

        public HashSet<Known> KnownProxies { get; set; } = new HashSet<Known>() 
        {
            new Known() { ip = "10.2.0.0", prefixLength = 16 },
            new Known() { ip = "192.168.0.0", prefixLength = 16 }
        };

        public bool real_ip_cf { get; set; }

        public ProxySettings proxy = new ProxySettings();

        public ProxySettings[] globalproxy = new ProxySettings[]
        {
            new ProxySettings()
            {
                pattern = "\\.onion",
                list = new string[] { "socks5://127.0.0.1:9050" }
            }
        };

        public IReadOnlyCollection<OverrideResponse> overrideResponse = new List<OverrideResponse>()
        {
            new OverrideResponse()
            {
                pattern = "/over/text",
                action = "html",
                type = "text/plain; charset=utf-8",
                val = "text"
            },
            new OverrideResponse()
            {
                pattern = "/over/online.js",
                action = "file",
                type = "application/javascript; charset=utf-8",
                val = "plugins/online.js"
            },
            new OverrideResponse()
            {
                pattern = "/over/gogoole",
                action = "redirect",
                val = "https://www.google.com/"
            }
        };
        #endregion


        #region SISI
        public SisiSettings BongaCams { get; set; } = new SisiSettings("BongaCams", "kwwsv=22hh1erqjdfdpv1frp")
        {
            spider = false,
            headers = HeadersModel.Init(
                ("referer", "{host}/"),
                ("x-requested-with", "XMLHttpRequest")
            ).ToDictionary()
        };

        public SisiSettings Runetki { get; set; } = new SisiSettings("Runetki", "kwwsv=22uxv1uxqhwnl81frp")
        {
            spider = false,
            headers = HeadersModel.Init(
                ("referer", "{host}/"),
                ("x-requested-with", "XMLHttpRequest")
            ).ToDictionary()
        };

        public SisiSettings Chaturbate { get; set; } = new SisiSettings("Chaturbate", "kwwsv=22fkdwxuedwh1frp")
        {
            spider = false,
        };

        public SisiSettings Ebalovo { get; set; } = new SisiSettings("Ebalovo", "kwwsv=22zzz1hedoryr1sur")
        {
            headers = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("sec-ch-ua", "\"Google Chrome\";v=\"135\", \"Not-A.Brand\";v=\"8\", \"Chromium\";v=\"135\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36")
            ).ToDictionary(),
            headers_stream = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("sec-ch-ua", "\"Google Chrome\";v=\"135\", \"Not-A.Brand\";v=\"8\", \"Chromium\";v=\"135\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "video"),
                ("sec-fetch-mode", "no-cors"),
                ("sec-fetch-site", "same-origin"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public SisiSettings Eporner { get; set; } = new SisiSettings("Eporner", "kwwsv=22zzz1hsruqhu1frp", streamproxy: true);

        public SisiSettings HQporner { get; set; } = new SisiSettings("HQporner", "kwwsv=22p1ktsruqhu1frp")
        {
            geostreamproxy = new string[] { "ALL" },
            headers = HeadersModel.Init("referer", "{host}").ToDictionary()
        };

        public SisiSettings Porntrex { get; set; } = new SisiSettings("Porntrex", "kwwsv=22zzz1sruqwuh{1frp", streamproxy: true)
        {
            headers_stream = HeadersModel.Init(
                ("referer", "{host}/")
            ).ToDictionary()
        };

        public SisiSettings Spankbang { get; set; } = new SisiSettings("Spankbang", "kwwsv=22ux1vsdqnedqj1frp");

        public SisiSettings Xhamster { get; set; } = new SisiSettings("Xhamster", "kwwsv=22ux1{kdpvwhu1frp")
        {
            headers = HeadersModel.Init(
                ("accept", "text/plain, */*; q=0.0"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("priority", "u=1, i"),
                ("sec-ch-ua", "\"Not(A: Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "cross-site"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public SisiSettings Xnxx { get; set; } = new SisiSettings("Xnxx", "kwwsv=22zzz1{q{{1frp");

        public SisiSettings Tizam { get; set; } = new SisiSettings("Tizam", "kwwsv=22lq1wl}dp1lqir", streamproxy: true);

        public SisiSettings Xvideos { get; set; } = new SisiSettings("Xvideos", "kwwsv=22zzz1{ylghrv1frp");

        public SisiSettings XvideosRED { get; set; } = new SisiSettings("XvideosRED", "kwwsv=22zzz1{ylghrv1uhg", enable: false);

        public SisiSettings PornHub { get; set; } = new SisiSettings("PornHub", "kwwsv=22uw1sruqkxe1frp", streamproxy: true)
        {
            headers = HeadersModel.Init(
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-site", "none"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1"),
                ("cookie", "platform=pc; bs=ukbqk2g03joiqzu68gitadhx5bhkm48j; ss=250837987735652383; fg_0d2ec4cbd943df07ec161982a603817e=56239.100000; atatusScript=hide; _gid=GA1.2.309162272.1686472069; d_fs=1; d_uidb=2f5e522a-fa28-a0fe-0ab2-fd90f45d96c0; d_uid=2f5e522a-fa28-a0fe-0ab2-fd90f45d96c0; d_uidb=2f5e522a-fa28-a0fe-0ab2-fd90f45d96c0; accessAgeDisclaimerPH=1; cookiesBannerSeen=1; _gat=1; __s=64858645-42FE722901BBA6E6-125476E1; __l=64858645-42FE722901BBA6E6-125476E1; hasVisited=1; fg_f916a4d27adf4fc066cd2d778b4d388e=78731.100000; fg_fa3f0973fd973fca3dfabc86790b408b=12606.100000; _ga_B39RFFWGYY=GS1.1.1686472069.1.1.1686472268.0.0.0; _ga=GA1.1.1515398043.1686472069")
            ).ToDictionary()
        };

        public SisiSettings PornHubPremium { get; set; } = new SisiSettings("PornHubPremium", "kwwsv=22uw1sruqkxesuhplxp1frp", streamproxy: true, enable: false)
        {
            headers = HeadersModel.Init(
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-site", "none"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1")
            ).ToDictionary()
        };
        #endregion

        #region Online
        /// <summary>
        /// https://iptv.online/ru/dealers/api
        /// </summary>
        public OnlinesSettings IptvOnline { get; set; } = new OnlinesSettings("IptvOnline", "https://iptv.online", enable: false);

        /// <summary>
        /// aHR0cHM6Ly92ZW92ZW8uaW8=
        /// </summary>
        public OnlinesSettings VeoVeo { get; set; } = new OnlinesSettings("VeoVeo", "kwwsv=22dsl1uvwsujdslsw1frp");

        public RezkaSettings Rezka { get; set; } = new RezkaSettings("Rezka", "kwwsv=22kguh}nd1ph", true) { hls = true, scheme = "http" };

        public RezkaSettings RezkaPrem { get; set; } = new RezkaSettings("RezkaPrem", null) { enable = false, hls = true, scheme = "http" };

        public CollapsSettings Collaps { get; set; } = new CollapsSettings("Collaps", "kwwsv=22dsl1ox{hpeg1zv", streamproxy: true, two: false)
        {
            two = true,
            apihost = "https://api.bhcesh.me",
            token = "eedefb541aeba871dcfc756e6b31c02e",
            headers = HeadersModel.Init(
                ("Origin", "{host}"),
                ("Referer", "{host}/"),
                ("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1")
            ).ToDictionary(),
            headers_stream = HeadersModel.Init(
                ("Origin", "{host}"),
                ("Referer", "{host}/"),
                ("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1")
            ).ToDictionary()
        };

        public OnlinesSettings Ashdi { get; set; } = new OnlinesSettings("Ashdi", "kwwsv=22edvh1dvkgl1yls") { geo_hide = new string[] { "RU", "BY" } };

        public OnlinesSettings Kinoukr { get; set; } = new OnlinesSettings("Kinoukr", "kwwsv=22nlqrxnu1frp")
        {
            geo_hide = new string[] { "RU", "BY" },
            headers = HeadersModel.Init
            (
                ("cache-control", "no-cache"),
                ("cookie", "legit_user=1;"),
                ("dnt", "1"),
                ("origin", "{host}"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("referer", "{host}/"),
                ("sec-ch-ua", "\"Not A(Brand\";v=\"8\", \"Chromium\";v=\"132\", \"Google Chrome\";v=\"132\""),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-mode", "navigate"),
                ("sec-fetch-site", "same-origin"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public OnlinesSettings Eneyida { get; set; } = new OnlinesSettings("Eneyida", "kwwsv=22hqh|lgd1wy");

        public OnlinesSettings Kinotochka { get; set; } = new OnlinesSettings("Kinotochka", "kwwsv=22nlqryleh1yls", streamproxy: true);

        public OnlinesSettings RutubeMovie { get; set; } = new OnlinesSettings("RutubeMovie", "kwwsv=22uxwxeh1ux", streamproxy: true)
        {
            headers = HeadersModel.Init(
                ("uuid", "a834f1e4-ebc7-435f-bb82-9aa11cc9e815")
            ).ToDictionary()
        };

        public OnlinesSettings Plvideo { get; set; } = new OnlinesSettings("Plvideo", "kwwsv=22dsl1j41soylghr1ux", streamproxy: true);

        public OnlinesSettings CDNvideohub { get; set; } = new OnlinesSettings("CDNvideohub", "kwwsv=22sodsl1fgqylghrkxe1frp", streamproxy: true)
        {
            headers = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("cache-control", "no-cache"),
                ("referer", "encrypt:kwwsv=22kgnlqr1sxe2"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("sec-ch-ua", "\"Google Chrome\";v=\"137\", \"Chromium\";v=\"137\", \"Not/A)Brand\";v=\"24\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "cross-site"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public OnlinesSettings Redheadsound { get; set; } = new OnlinesSettings("Redheadsound", "kwwsv=22uhgkhdgvrxqg1vwxglr")
        {
            headers = HeadersModel.Init("referer", "{host}/").ToDictionary()
        };

        public OnlinesSettings iRemux { get; set; } = new OnlinesSettings("iRemux", "kwwsv=22phjdreodnr1frp", streamproxy: true) { corseu = true };

        public PidTorSettings PidTor { get; set; } = new PidTorSettings() { enable = true, redapi = "http://redapi.cfhttp.top", min_sid = 15 };

        public FilmixSettings Filmix { get; set; } = new FilmixSettings("Filmix", "kwws=22ilopl{dss1f|rx")
        {
            headers = HeadersModel.Init(
                ("Accept-Encoding", "gzip")
            ).ToDictionary()
        };

        public FilmixSettings FilmixTV { get; set; } = new FilmixSettings("FilmixTV", "kwwsv=22dsl1ilopl{1wy", enable: false)
        {
            pro = true,
            headers = HeadersModel.Init(
                ("user-agent", "Mozilla/5.0 (SMART-TV; LINUX; Tizen 6.0) AppleWebKit/537.36 (KHTML, like Gecko) 76.0.3809.146/6.0 TV Safari/537.36")
            ).ToDictionary()
        };

        public FilmixSettings FilmixPartner { get; set; } = new FilmixSettings("FilmixPartner", "kwws=22819418914;2sduwqhubdsl", enable: false);

        /// <summary>
        /// aHR0cHM6Ly9nby56ZXQtZmxpeC5vbmxpbmU=
        /// aHR0cHM6Ly9nby56ZXRmbGl4LW9ubGluZS5sb2w=
        /// aHR0cHM6Ly96ZXRmbGl4LW9ubGluZS5hcnQv
        /// </summary>
        public ZetflixSettings Zetflix { get; set; } = new ZetflixSettings("Zetflix", "kwwsv=22jr1}hw0iol{1rqolqh")
        {
            browser_keepopen = true,
            geostreamproxy = new string[] { "ALL" },
            hls = true
        };

        /// <summary>
        /// aHR0cHM6Ly9raW5vcGxheTIuc2l0ZS8=
        /// a2lub2dvLm1lZGlh
        /// aHR0cHM6Ly9maWxtLTIwMjQub3JnLw==
        /// </summary>
        public OnlinesSettings VideoDB { get; set; } = new OnlinesSettings("VideoDB", "kwwsv=22nlqrjr1phgld", "kwwsv=2263ei6:<31reuxw1vkrz", streamproxy: true)
        {
            imitationHuman = true,
            headers = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                ("dnt", "1"),
                ("cache-control", "no-cache"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("sec-ch-ua", "Google Chrome\";v=\"135\", \"Not-A.Brand\";v=\"8\", \"Chromium\";v=\"135"),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-storage-access", "active"),
                ("upgrade-insecure-requests", "1"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36")
            ).ToDictionary(),
            headers_stream = HeadersModel.Init(
                ("accept", "*/*"),
                ("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                ("dnt", "1"),
                ("cache-control", "no-cache"),
                ("pragma", "no-cache"),
                ("origin", "{host}"),
                ("referer", "{host}/"),
                ("sec-ch-ua", "Google Chrome\";v=\"135\", \"Not-A.Brand\";v=\"8\", \"Chromium\";v=\"135"),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "same-site"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        /// <summary>
        /// aHR0cHM6Ly9jb2xkZmlsbS5pbmsv
        /// </summary>
        public OnlinesSettings CDNmovies { get; set; } = new OnlinesSettings("CDNmovies", "kwwsv=22frogfgq1{|}")
        {
            headers = HeadersModel.Init(
                ("DNT", "1"),
                ("Upgrade-Insecure-Requests", "1")
            ).ToDictionary()
        };

        public OnlinesSettings VDBmovies { get; set; } = new OnlinesSettings("VDBmovies", "kwwsv=22fgqprylhv0vwuhdp1rqolqh")
        {
            geostreamproxy = new string[] { "ALL" },
            headers = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                ("dnt", "1"),
                ("cache-control", "no-cache"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("sec-ch-ua", "Google Chrome\";v=\"135\", \"Not-A.Brand\";v=\"8\", \"Chromium\";v=\"135"),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-storage-access", "active"),
                ("upgrade-insecure-requests", "1"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public OnlinesSettings FanCDN { get; set; } = new OnlinesSettings("FanCDN", "kwwsv=22p|idqvhuldo1qhw", streamproxy: true)
        {
            enable = false,
            imitationHuman = true,
            headers = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                ("dnt", "1"),
                ("cache-control", "no-cache"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("sec-ch-ua", "Google Chrome\";v=\"135\", \"Not-A.Brand\";v=\"8\", \"Chromium\";v=\"135"),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-storage-access", "active"),
                ("upgrade-insecure-requests", "1"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36")
            ).ToDictionary(),
            headers_stream = HeadersModel.Init(
                ("accept", "*/*"),
                ("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                ("dnt", "1"),
                ("cache-control", "no-cache"),
                ("pragma", "no-cache"),
                ("origin", "encrypt:kwwsv=22idqfgq1qhw"),
                ("referer", "encrypt:kwwsv=22idqfgq1qhw2"),
                ("sec-ch-ua", "Google Chrome\";v=\"135\", \"Not-A.Brand\";v=\"8\", \"Chromium\";v=\"135"),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "same-site"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public KinobaseSettings Kinobase { get; set; } = new KinobaseSettings("Kinobase", "kwwsv=22nlqredvh1ruj", true, hdr: true) 
        { 
            geostreamproxy = new string[] { "ALL" } 
        };

        /// <summary>
        /// Получение учетной записи
        /// 
        /// tg: @monk_in_a_hat
        /// email: helpdesk@lumex.ink
        /// </summary>
        public LumexSettings VideoCDN { get; set; } = new LumexSettings("VideoCDN", "https://api.lumex.site", "API-токен", "https://portal.lumex.host", "ID клиент")
        {
            enable = false,
            log = false,
            verifyip = true, // ссылки привязаны к ip пользователя
            scheme = "http",
            geostreamproxy = new string[] { "UA" },
            hls = false, // false - mp4 / true - m3u8
            disable_protection = false, // true - отключить проверку на парсер
            disable_ads = false, // отключить рекламу
            vast = new VastConf() { msg = "Реклама от VideoCDN" }
        };

        public LumexSettings Lumex { get; set; } = new LumexSettings("Lumex", "kwwsv=22sruwdo1oxph{1krvw", null, "lumex.space", "tl6h28Hn1rL5")
        {
            enable = true,
            hls = true,
            scheme = "http",
            geostreamproxy = new string[] { "ALL" }
        };

        public VokinoSettings VoKino { get; set; } = new VokinoSettings("VoKino", "kwws=22dsl1yrnlqr1wy", streamproxy: true);

        public IframeVideoSettings IframeVideo { get; set; } = new IframeVideoSettings("IframeVideo", "kwwsv=22liudph1ylghr", "kwwsv=22ylghriudph1vsdfh", enable: false);

        /// <summary>
        /// aHR0cHM6Ly92aWQxNzMwODAxMzcwLmZvdHBybzEzNWFsdG8uY29tL2FwaS9pZGtwP2twX2lkPTEzOTI1NTAmZD1raW5vZ28uaW5j
        /// </summary>
        public OnlinesSettings HDVB { get; set; } = new OnlinesSettings("HDVB", "kwwsv=22dslye1frp", token: "8h5ih7f:3edig<d:747f7i4:3hh4e4<5");

        /// <summary>
        /// aHR0cHM6Ly92aWJpeC5vcmcvYXBpL2V4dGVybmFsL2RvY3VtZW50YXRpb24=
        /// </summary>
        public OnlinesSettings Vibix { get; set; } = new OnlinesSettings("Vibix", "kwwsv=22ylel{1ruj", token: "2281|zjwU6jDRmNwgdoRYkQ2ySJoyu1rXiwj8qJBMN9M36bc52415", streamproxy: true);

        /// <summary>
        /// aHR0cHM6Ly92aWRlb3NlZWQudHYvZmFxLnBocA==
        /// </summary>
        public OnlinesSettings Videoseed { get; set; } = new OnlinesSettings("Videoseed", "kwwsv=22ylghrvhhg1wy", streamproxy: true, enable: false)
        {
            priorityBrowser = "http",
            headers = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                ("dnt", "1"),
                ("cache-control", "no-cache"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("sec-ch-ua", "Google Chrome\";v=\"135\", \"Not-A.Brand\";v=\"8\", \"Chromium\";v=\"135"),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-storage-access", "active"),
                ("upgrade-insecure-requests", "1"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36")
            ).ToDictionary(),
            headers_stream = HeadersModel.Init(
                ("accept", "*/*"),
                ("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                ("dnt", "1"),
                ("cache-control", "no-cache"),
                ("pragma", "no-cache"),
                ("referer", "encrypt:kwwsv=22wy040nlqrvhuldo1qhw2"),
                ("sec-ch-ua", "Google Chrome\";v=\"135\", \"Not-A.Brand\";v=\"8\", \"Chromium\";v=\"135"),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "video"),
                ("sec-fetch-mode", "no-cors"),
                ("sec-fetch-site", "cross-site"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        /// <summary>
        /// kwwsv=22dsl1vuyns1frp
        /// kwwsv=22nsdss1olqn2dsl
        /// </summary>
        public KinoPubSettings KinoPub { get; set; } = new KinoPubSettings("KinoPub", "kwwsv=22dsl1vuyns1frp")
        {
            filetype = "hls",
            headers = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-mode", "navigate"),
                ("sec-fetch-site", "none"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public AllohaSettings Alloha { get; set; } = new AllohaSettings("Alloha", "kwwsv=22dsl1dsexjdoo1ruj", "kwwsv=22wruvr0dv1doodunqrz1rqolqh", "", "", true, true);

        public AllohaSettings Mirage { get; set; } = new AllohaSettings("Mirage", "kwwsv=22dsl1dsexjdoo1ruj", "kwwsv=22txdguloolrq0dv1doodunqrz1rqolqh", "6892d506bbdd5790e0ca047ff39462", "", true, true)
        {
            streamproxy = true,
            reserve = true,
            headers = HeadersModel.Init(
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("pragma", "no-cache"),
                ("priority", "u=1, i"),
                ("sec-ch-ua", "\"Not)A;Brand\";v=\"8\", \"Chromium\";v=\"138\", \"Google Chrome\";v=\"138\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-storage-access", "active"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public OnlinesSettings GetsTV { get; set; } = new OnlinesSettings("GetsTV", "https://getstv.com", enable: false) 
        {
            headers = HeadersModel.Init(
                ("user-agent", "Mozilla/5.0 (Web0S; Linux/SmartTV) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/53.0.2785.34 Safari/537.36 WebAppManager")
            ).ToDictionary()
        };
        #endregion

        #region ENG
        /// <summary>
        /// aHR0cHM6Ly93d3cuaHlkcmFmbGl4LnZpcA==
        /// </summary>
        public OnlinesSettings Hydraflix { get; set; } = new OnlinesSettings("Hydraflix", "kwwsv=22ylgidvw1sur", streamproxy: true);

        /// <summary>
        /// aHR0cHM6Ly92aWRzcmMueHl6
        /// aHR0cHM6Ly92aWRzcmMucHJv
        /// aHR0cHM6Ly92aWRzcmMudG8=
        /// </summary>
        public OnlinesSettings Vidsrc { get; set; } = new OnlinesSettings("Vidsrc", "kwwsv=22ylgvuf1ff", streamproxy: true);

        public OnlinesSettings MovPI { get; set; } = new OnlinesSettings("MovPI", "kwwsv=22prylhvdsl1foxe", streamproxy: true);

        /// <summary>
        /// aHR0cHM6Ly9kYXkyc29hcC54eXov
        /// </summary>
        public OnlinesSettings VidLink { get; set; } = new OnlinesSettings("VidLink", "kwwsv=22ylgolqn1sur", streamproxy: true);

        public OnlinesSettings Videasy { get; set; } = new OnlinesSettings("Videasy", "kwwsv=22sod|hu1ylghdv|1qhw", streamproxy: true);

        /// <summary>
        /// aHR0cHM6Ly9zbWFzaHlzdHJlYW0ueHl6
        /// </summary>
        public OnlinesSettings Smashystream { get; set; } = new OnlinesSettings("Smashystream", "kwwsv=22sod|hu1vpdvk|vwuhdp1frp", streamproxy: true);

        public OnlinesSettings Twoembed { get; set; } = new OnlinesSettings("Twoembed", "kwwsv=22hpehg1vx", streamproxy: true, enable: false)
        {
            headers_stream = HeadersModel.Init(
                ("accept", "*/*"),
                ("accept-language", "en-US,en;q=0.5"),
                ("referer", "{host}/"),
                ("origin", "{host}"),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "cross-site")
            ).ToDictionary()
        };

        public OnlinesSettings Autoembed { get; set; } = new OnlinesSettings("Autoembed", "kwwsv=22sod|hu1dxwrhpehg1ff", enable: false)
        {
            priorityBrowser = "http",
            headers_stream = HeadersModel.Init(
                ("accept", "*/*"),
                ("accept-language", "en-US,en;q=0.5"),
                ("referer", "{host}/"),
                ("origin", "{host}"),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "cross-site")
            ).ToDictionary()
        };

        public OnlinesSettings Rgshows { get; set; } = new OnlinesSettings("Rgshows", "kwwsv=22dsl1ujvkrzv1ph", streamproxy: true, enable: false)
        {
            headers = HeadersModel.Init(
                ("accept", "*/*"),
                ("user-agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1"),
                ("accept-language", "en-US,en;q=0.5"),
                ("referer", "encrypt:kwwsv=22zzz1ujvkrzv1ph2"),
                ("origin", "encrypt:kwwsv=22zzz1ujvkrzv1ph"),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "same-site")
            ).ToDictionary(),
            headers_stream = HeadersModel.Init(
                ("accept", "*/*"),
                ("user-agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1"),
                ("accept-language", "en-US,en;q=0.5"),
                ("referer", "encrypt:kwwsv=22zzz1ujvkrzv1ph2"),
                ("origin", "encrypt:kwwsv=22zzz1ujvkrzv1ph"),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "cross-site")
            ).ToDictionary()
        };

        public OnlinesSettings Playembed { get; set; } = new OnlinesSettings("Playembed", "https://vidora.su", enable: false);
        #endregion

        #region Anime
        public KodikSettings Kodik { get; set; } = new KodikSettings("Kodik", "kwwsv=22nrglndsl1frp", "kwwsv=22nrgln1lqir", "hh438g49<d<7g87dhe7hgh<6f6f4935:", "", true)
        {
            cdn_is_working = true,
            geostreamproxy = new string[] { "UA" },
            headers = HeadersModel.Init(
                ("referer", "encrypt:kwwsv=22dqlole1ph2")
            ).ToDictionary()
        };

        /// <summary>
        /// move to AniLiberty
        /// </summary>
        public OnlinesSettings AnilibriaOnline { get; set; } = new OnlinesSettings("AnilibriaOnline", "kwwsv=22dsl1dqloleuld1wy", enable: false);

        public OnlinesSettings AniLiberty { get; set; } = new OnlinesSettings("AniLiberty", "kwwsv=22dsl1dqloleuld1dss");

        /// <summary>
        /// aHR0cHM6Ly9hbmlsaWIubWU=
        /// </summary>
        public OnlinesSettings AnimeLib { get; set; } = new OnlinesSettings("AnimeLib", "kwwsv=22dsl1fgqolev1ruj")
        {
            enable = false,
            headers = HeadersModel.Init(
                ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("origin", "encrypt:kwwsv=22dqlole1ph"),
                ("referer", "encrypt:kwwsv=22dqlole1ph2"),
                ("pragma", "no-cache"),
                ("priority", "u=0, i"),
                ("sec-ch-ua", "\"Chromium\";v=\"134\", \"Not:A-Brand\";v=\"24\", \"Google Chrome\";v=\"134\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "cross-site"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36")
            ).ToDictionary(),
            headers_stream = HeadersModel.Init(
                ("accept", "*/*"),
                ("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("origin", "encrypt:kwwsv=22dqlole1ph"),
                ("referer", "encrypt:kwwsv=22dqlole1ph2"),
                ("pragma", "no-cache"),
                ("priority", "i"),
                ("sec-ch-ua", "\"Chromium\";v=\"134\", \"Not:A-Brand\";v=\"24\", \"Google Chrome\";v=\"134\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "video"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "same-site"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36")
            ).ToDictionary()
        };

        public OnlinesSettings AniMedia { get; set; } = new OnlinesSettings("AniMedia", "kwwsv=22dphgld1rqolqh");

        public OnlinesSettings Animevost { get; set; } = new OnlinesSettings("Animevost", "kwwsv=22dqlphyrvw1ruj", streamproxy: true);

        public OnlinesSettings MoonAnime { get; set; } = new OnlinesSettings("MoonAnime", "kwwsv=22dsl1prrqdqlph1duw", token: ";98iHI0H5h4Ef05fd7640h9D4830:;3GIG0:6:F9E") { geo_hide = new string[] { "RU", "BY" } };

        public OnlinesSettings Animebesst { get; set; } = new OnlinesSettings("Animebesst", "kwwsv=22dqlph41ehvw");

        public OnlinesSettings AnimeGo { get; set; } = new OnlinesSettings("AnimeGo", "kwwsv=22dqlphjr1ph", streamproxy: true, enable: false)
        {
            headers_stream = HeadersModel.Init(
                ("origin", "https://aniboom.one"),
                ("referer", "https://aniboom.one/")
            ).ToDictionary()
        };
        #endregion
    }
}

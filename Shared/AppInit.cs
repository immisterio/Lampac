using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using System;
using Lampac.Models.Module;
using System.Reflection;
using Lampac.Models.AppConf;
using Lampac.Models.DLNA;
using Lampac.Models.Merchant;
using System.Collections.Concurrent;
using Shared.Model.Base;
using System.Text.RegularExpressions;
using Shared.Models.AppConf;
using Shared.Models.ServerProxy;
using Lampac.Engine.CORE;
using Shared.Models.Browser;

namespace Lampac
{
    public class AppInit : Shared.Model.AppInit
    {
        static AppInit() { LoadModules(); }

        #region conf
        public static(AppInit, DateTime) cacheconf = default;

        public static AppInit conf
        {
            get
            {
                if (!File.Exists("init.conf"))
                    return new AppInit();

                var lastWriteTime = File.GetLastWriteTime("init.conf");

                if (cacheconf.Item2 != lastWriteTime)
                {
                    var jss = new JsonSerializerSettings { Error = (se, ev) => 
                    { 
                        ev.ErrorContext.Handled = true;
                        Console.WriteLine($"DeserializeObject Exception init.conf:\n{ev.ErrorContext.Error}\n\n"); 
                    }};

                    string initfile = File.ReadAllText("init.conf").Trim();
                    initfile = Regex.Replace(initfile, "\"weblog\":([ \t]+)?(true|false)([ \t]+)?,", "", RegexOptions.IgnoreCase);

                    if (!initfile.StartsWith("{"))
                        initfile = "{" + initfile + "}";

                    try
                    {
                        cacheconf.Item2 = lastWriteTime;
                        cacheconf.Item1 = JsonConvert.DeserializeObject<AppInit>(initfile, jss);
                    }
                    catch
                    {
                        if (cacheconf != default)
                            return cacheconf.Item1;
                    }

                    if (cacheconf.Item1 == null)
                        cacheconf.Item1 = new AppInit();

                    if (cacheconf.Item1 != null)
                    {
                        if (!string.IsNullOrEmpty(cacheconf.Item1.corsehost))
                            corseuhost = cacheconf.Item1.corsehost;

                        _vast = cacheconf.Item1.vast;
                        _defaultOn = cacheconf.Item1.defaultOn;
                        PosterApi.Initialization(cacheconf.Item1.omdbapi_key, cacheconf.Item1.posterApi, new ProxyLink());
                    }

                    #region accounts
                    if (cacheconf.Item1.accsdb.accounts != null)
                    {
                        foreach (var u in cacheconf.Item1.accsdb.accounts)
                        {
                            if (cacheconf.Item1.accsdb.findUser(u.Key) is AccsUser user)
                            {
                                if (u.Value > user.expires)
                                    user.expires = u.Value;
                            }
                            else
                            {
                                cacheconf.Item1.accsdb.users.Add(new AccsUser()
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

                                        if (cacheconf.Item1.accsdb.findUser(email) is AccsUser user)
                                        {
                                            if (e > user.expires)
                                                user.expires = e;

                                            user.group = conf.Merchant.defaultGroup;
                                        }
                                        else
                                        {
                                            cacheconf.Item1.accsdb.users.Add(new AccsUser()
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
                        File.WriteAllText("current.conf", JsonConvert.SerializeObject(cacheconf.Item1, Formatting.Indented));
                    }
                    catch { }
                }

                return cacheconf.Item1;
            }
        }

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

        public static bool Win32NT => Environment.OSVersion.Platform == PlatformID.Win32NT;


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

        public bool pirate_store = true;

        public string apikey = null;

        public bool litejac = true;

        public bool filelog = false;

        public bool disableEng = false;

        public string anticaptchakey;

        public string omdbapi_key;

        public string playerInner;

        public Dictionary<string, CmdConf> cmd = new Dictionary<string, CmdConf>();

        public PosterApiConf posterApi = new PosterApiConf() 
        {
            rsize = true, width = 210,
            bypass = "statichdrezka\\."
        };

        public KitConf kit = new KitConf() { cacheToSeconds = 20 };

        public SyncConf sync = new SyncConf();

        public WebLogConf weblog = new WebLogConf();

        public OpenStatConf openstat = new OpenStatConf();

        public RchConf rch = new RchConf() { enable = true, keepalive = 45, permanent_connection = true };

        public StorageConf storage = new StorageConf() { enable = true, max_size = 7_000000, brotli = false, md5name = true };

        public PuppeteerConf chromium = new PuppeteerConf() 
        { 
            enable = true, Xvfb = true,
            Args = new string[] { "--disable-blink-features=AutomationControlled" },
            context = new KeepopenContext() { keepopen = true, keepalive = 20, min = 0, max = 4 }
        };

        public PuppeteerConf firefox = new PuppeteerConf()
        {
            enable = false, Headless = true,
            context = new KeepopenContext() { keepopen = true, keepalive = 20, min = 1, max = 2 }
        };

        public FfprobeSettings ffprobe = new FfprobeSettings() { enable = true };

        public CubConf cub { get; set; } = new CubConf()
        {
            enable = false, 
            domain = CrypTo.DecodeBase64("Y3ViLnJlZA=="), scheme = "https",
            mirror = "mirror-kurwa.men",
            cache_api = 20, cache_img = 60,
        };

        public TmdbConf tmdb { get; set; } = new TmdbConf()
        {
            enable = true,
            DNS = "9.9.9.9", DNS_TTL = 20,
            cache_api = 20, cache_img = -1, check_img = false,
            api_key = "4ef0d7355d9ffb5151e987764708ce96"
        };

        public ServerproxyConf serverproxy = new ServerproxyConf()
        {
            enable = true, encrypt = true, verifyip = true,
            buffering = new ServerproxyBufferingConf()
            {
                enable = true, rent = 8192, length = 3906, millisecondsTimeout = 5
            },
            image = new ServerproxyImageConf()
            {
                cache = false, cache_rsize = true
            },
            maxlength_m3u = 1900000,
            maxlength_ts = 10000000
        };

        public FileCacheConf fileCacheInactive = new FileCacheConf() 
        { 
            maxcachesize = 10_000, // 10GB на папку
            img = 20, hls = 90, html = 5, torrent = 2880 // minute
        };

        public DLNASettings dlna = new DLNASettings() 
        { 
            enable = true, path = "dlna",
            uploadSpeed = 125000 * 10,
            autoupdatetrackers = true, intervalUpdateTrackers = 90, addTrackersToMagnet = true, 
            cover = new CoverSettings() 
            {
                enable = true, preview = true, 
                timeout = 20, skipModificationTime = 60 * 5,
                priorityClass = System.Diagnostics.ProcessPriorityClass.Idle,
                extension = "(mp4|mkv|avi|mpg|mpe|mpv)",
                coverComand = "-n -ss 3:00 -i \"{file}\" -vf \"thumbnail=150,scale=240:-2\" -frames:v 1 \"{thumb}\"",
                // фрэмрэйт -r   : больше - больше файл , плавнее движения
                // качество -crf : больше - меньше файл , появляются артефакты
                previewComand = "-n -ss 00:03:00 -to 00:03:07 -i \"{file}\" -ss 00:04:37 -to 00:04:44 -i \"{file}\" -ss 00:06:41 -to 00:06:48 -i \"{file}\" -ss 00:09:20 -to 00:09:27 -i \"{file}\" -ss 00:12:45 -to 00:12:52 -i \"{file}\" -ss 00:17:09 -to 00:17:16 -i \"{file}\" -ss 00:22:50 -to 00:22:57 -i \"{file}\" -ss 00:29:51 -to 00:29:58 -i \"{file}\" -ss 00:39:23 -to 00:39:30 -i \"{file}\" -ss 00:51:44 -to 00:51:51 -i \"{file}\" -ss 01:07:45 -to 01:07:52 -i \"{file}\" -ss 01:20:33 -to 01:20:40 -i \"{file}\" -filter_complex \"[0:v]setpts=0.5*PTS,scale=-2:240[v0];[1:v]setpts=0.5*PTS,scale=-2:240[v1];[2:v]setpts=0.5*PTS,scale=-2:240[v2];[3:v]setpts=0.5*PTS,scale=-2:240[v3];[4:v]setpts=0.5*PTS,scale=-2:240[v4];[5:v]setpts=0.5*PTS,scale=-2:240[v5];[6:v]setpts=0.5*PTS,scale=-2:240[v6];[7:v]setpts=0.5*PTS,scale=-2:240[v7];[8:v]setpts=0.5*PTS,scale=-2:240[v8];[9:v]setpts=0.5*PTS,scale=-2:240[v9];[10:v]setpts=0.5*PTS,scale=-2:240[v10];[11:v]setpts=0.5*PTS,scale=-2:240[v11];[v0][v1][v2][v3][v4][v5][v6][v7][v8][v9][v10][v11]concat=n=12:v=1:a=0\" -an -r 20 -c:v libx264 -crf 28 -preset veryslow \"{preview}\""
            }
        };

        public WebConf LampaWeb = new WebConf()
        {
            autoupdate = true,
            intervalupdate = 90,
            basetag = true, index = "lampa-main/index.html",
            tree = "5cea48fdcb8aab7bd14d2925e2a15cb714500e99"
        };

        public OnlineConf online = new OnlineConf()
        {
            findkp = "all", checkOnlineSearch = true, 
            spider = true, spiderName = "Spider",
            component = "lampac", name = "Lampac", description = "Плагин для просмотра онлайн сериалов и фильмов",
            version = true, btn_priority_forced = true, showquality = true
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

        public HashSet<Known> KnownProxies { get; set; } = new HashSet<Known>() 
        {
            new Known() { ip = "10.2.0.0", prefixLength = 16 },
            new Known() { ip = "192.168.0.0", prefixLength = 16 }
        };

        public bool real_ip_cf { get; set; }

        public ProxySettings proxy = new ProxySettings();

        public ConcurrentBag<ProxySettings> globalproxy = new ConcurrentBag<ProxySettings>()
        {
            new ProxySettings()
            {
                pattern = "\\.onion",
                list = new ConcurrentBag<string>() { "socks5://127.0.0.1:9050" }
            }
        };


        public ConcurrentBag<OverrideResponse> overrideResponse = new ConcurrentBag<OverrideResponse>()
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
    }
}

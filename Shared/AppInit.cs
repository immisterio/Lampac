﻿using Microsoft.AspNetCore.Http;
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
                        Console.WriteLine("init.conf - " + ev.ErrorContext.Error + "\n\n"); 
                    }};

                    string initfile = File.ReadAllText("init.conf").Trim();
                    initfile = Regex.Replace(initfile, "\"weblog\":([ \t]+)?(true|false)([ \t]+)?,", "", RegexOptions.IgnoreCase);

                    if (!initfile.StartsWith("{"))
                        initfile = "{" + initfile + "}";

                    try
                    {
                        cacheconf.Item1 = JsonConvert.DeserializeObject<AppInit>(initfile, jss);
                    }
                    catch { }

                    if (cacheconf.Item1 == null)
                        cacheconf.Item1 = new AppInit();

                    cacheconf.Item2 = lastWriteTime;

                    if (cacheconf.Item1 != null)
                    {
                        if (!string.IsNullOrEmpty(cacheconf.Item1.corsehost))
                            corseuhost = cacheconf.Item1.corsehost;

                        _vast = cacheconf.Item1.vast;
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

                            if (mod.initspace != null && mod.assembly.GetType(mod.initspace) is Type t && t.GetMethod("loaded") is MethodInfo m)
                                m.Invoke(null, new object[] { });

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

        public string localhost = "127.0.0.1";

        public bool multiaccess = false;

        public bool mikrotik = false;

        public string typecache = "hybrid"; // mem|file|hybrid

        public bool pirate_store = true;

        public string apikey = null;

        public bool litejac = true;

        public bool filelog = false;

        public string anticaptchakey;

        public KitConf kit = new KitConf();

        public SyncConf sync = new SyncConf();

        public WebLogConf weblog = new WebLogConf();

        public RchConf rch = new RchConf() { enable = true, keepalive = 45, permanent_connection = true };

        public StorageConf storage = new StorageConf() { enable = true, max_size = 7_000000, brotli = false, md5name = true };

        public PuppeteerConf puppeteer = new PuppeteerConf() { enable = true, keepopen = true };

        public FfprobeSettings ffprobe = new FfprobeSettings() { enable = true };

        public ServerproxyConf serverproxy = new ServerproxyConf()
        {
            enable = true, encrypt = true, verifyip = true, allow_tmdb = true,
            buffering = new ServerproxyBufferingConf()
            {
                enable = true, rent = 8192, length = 3906, millisecondsTimeout = 5
            },
            cache = new ServerproxyCacheConf()
            {
                img = false, img_rsize = true
            },
            tmdb = new ServerproxyTmdb() 
            {
                proxy = new ProxySettings()
                {
                    list = new ConcurrentBag<string>() { "socks5://127.0.0.1:9050" }
                }
            }
        };

        public FileCacheConf fileCacheInactive = new FileCacheConf() { maxcachesize = 400, intervalclear = 4, img = 10, hls = 90, html = 5, torrent = 50 };

        public DLNASettings dlna = new DLNASettings() 
        { 
            enable = true, allowedEncryption = true, path = "dlna",
            autoupdatetrackers = true, addTrackersToMagnet = true, intervalUpdateTrackers = 90
        };

        public WebConf LampaWeb = new WebConf() { autoupdate = true, intervalupdate = 90, basetag = true, index = "lampa-main/index.html" };

        public OnlineConf online = new OnlineConf() 
        { 
            findkp = "all", checkOnlineSearch = true,
            component = "lampac", name = "Lampac", description = "Плагин для просмотра онлайн сериалов и фильмов", version = true
        };

        public AccsConf accsdb = new AccsConf() 
        { 
            authMesage = "Войдите в аккаунт cub.red",
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

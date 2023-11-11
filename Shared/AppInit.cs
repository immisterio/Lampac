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

namespace Lampac
{
    public class AppInit : Shared.Model.AppInit
    {
        #region conf
        static (AppInit, DateTime) cacheconf = default;

        public static AppInit conf
        {
            get
            {
                if (!File.Exists("init.conf"))
                    return new AppInit();

                var lastWriteTime = File.GetLastWriteTime("init.conf");

                if (cacheconf.Item2 != lastWriteTime)
                {
                    cacheconf.Item1 = JsonConvert.DeserializeObject<AppInit>(File.ReadAllText("init.conf"));
                    cacheconf.Item2 = lastWriteTime;

                    if (cacheconf.Item1 != null)
                    {
                        if (!string.IsNullOrEmpty(cacheconf.Item1.corsehost))
                            corseuhost = cacheconf.Item1.corsehost;
                    }

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
                                    DateTime e = DateTime.FromFileTimeUtc(ex);
                                    string email = data[0].Trim().ToLower();

                                    if (cacheconf.Item1.accsdb.accounts.TryGetValue(email, out DateTime _ex))
                                    {
                                        if (e > _ex)
                                            cacheconf.Item1.accsdb.accounts[email] = e;
                                    }
                                    else
                                        cacheconf.Item1.accsdb.accounts.TryAdd(email, e);
                                }
                            }
                        }
                    }
                }

                return cacheconf.Item1;
            }
        }

        public static string Host(HttpContext httpContext) => $"{httpContext.Request.Scheme}://{(string.IsNullOrWhiteSpace(conf.listenhost) ? httpContext.Request.Host.Value : conf.listenhost)}";
        #endregion

        #region modules
        public static List<RootModule> modules;

        static AppInit()
        {
            if (File.Exists("module/manifest.json"))
            {
                modules = new List<RootModule>();

                foreach (var mod in JsonConvert.DeserializeObject<List<RootModule>>(File.ReadAllText("module/manifest.json")))
                {
                    if (!mod.enable)
                        continue;

                    string path = File.Exists(mod.dll) ? mod.dll : $"{Environment.CurrentDirectory}/module/{mod.dll}";
                    if (File.Exists(path))
                    {
                        mod.assembly = Assembly.LoadFile(path);

                        try
                        {
                            if (mod.initspace != null && mod.assembly.GetType(mod.initspace) is Type t && t.GetMethod("loaded") is MethodInfo m)
                                m.Invoke(null, new object[] { });
                        }
                        catch { }

                        modules.Add(mod);
                    }
                }
            }
        }
        #endregion


        public string listenip = "any";

        public int listenport = 9118;

        public string listenhost = null;

        public string apikey = null;

        public bool multiaccess = false;

        public bool log = false;

        public string anticaptchakey;


        public FfprobeSettings ffprobe = new FfprobeSettings() { enable = true, os = Environment.OSVersion.Platform == PlatformID.Unix ? "linux" : "win" };

        public ServerproxyConf serverproxy = new ServerproxyConf() { enable = true, encrypt = true, verifyip = true, allow_tmdb = true };

        public CronTime crontime = new CronTime() { updateLampaWeb = 20, clearCache = 60, updateTrackers = 120 };


        public FileCacheConf fileCacheInactiveDay = new FileCacheConf() { html = 1, img = 1, torrent = 2 };

        public DLNASettings dlna = new DLNASettings() { enable = true };

        public WebConf LampaWeb = new WebConf() { autoupdate = true, index = "lampa-main/index.html" };

        public OnlineConf online = new OnlineConf() { findkp = "all", checkOnlineSearch = true };

        public AccsConf accsdb = new AccsConf() { cubMesage = "Войдите в аккаунт", denyMesage = "Добавьте {account_email} в init.conf", maxiptohour = 10 };

        public MerchantsModel Merchant = new MerchantsModel();

        public HashSet<Known> KnownProxies { get; set; }

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

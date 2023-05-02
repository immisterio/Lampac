using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using Lampac.Models.Module;
using System.Reflection;
using Lampac.Models.JAC;
using Lampac.Models.AppConf;
using Lampac.Models;
using Lampac.Models.DLNA;
using Lampac.Models.Merchant;

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
                    string init = File.ReadAllText("init.conf");

                    // fix tracks.js
                    init = Regex.Replace(init, "\"ffprobe\": ?\"([^\"]+)?\"[^\n\r]+", "");

                    cacheconf.Item1 = JsonConvert.DeserializeObject<AppInit>(init);
                    cacheconf.Item2 = lastWriteTime;

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
                                    cacheconf.Item1.accsdb.accounts.Add(data[0].Trim().ToLower());
                            }
                        }
                    }
                }

                return cacheconf.Item1;
            }
        }

        public static string Host(HttpContext httpContext) => $"{httpContext.Request.Scheme}://{(string.IsNullOrWhiteSpace(conf.listenhost) ? httpContext.Request.Host.Value : conf.listenhost)}";

        public static string corseuhost => "https://cors.eu.org";
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

        public FfprobeSettings ffprobe = new FfprobeSettings() { enable = true, os = "linux" };

        public ServerproxyConf serverproxy = new ServerproxyConf() { enable = true, encrypt = true, allow_tmdb = true };

        public CronTime crontime = new CronTime() { updateLampaWeb = 20, clearCache = 60, updateTrackers = 120 };

        public bool multiaccess = false;

        public bool log = false;

        public string anticaptchakey;


        public FileCacheConf fileCacheInactiveDay = new FileCacheConf() { html = 3, img = 1, torrent = 10 };

        public DLNASettings dlna = new DLNASettings() { enable = true };

        public WebConf LampaWeb = new WebConf() { autoupdate = true, index = "lampa-main/index.html" };

        public SisiConf sisi = new SisiConf() { heightPicture = 200 };

        public OnlineConf online = new OnlineConf() { findkp = "alloha", checkOnlineSearch = true };

        public AccsConf accsdb = new AccsConf() { cubMesage = "Войдите в аккаунт", denyMesage = "Добавьте {account_email} в init.conf", maxiptohour = 10 };

        public MerchantsModel Merchant = new MerchantsModel();

        public HashSet<Known> KnownProxies { get; set; }

        public JacConf jac = new JacConf();


        public TrackerSettings Rutor = new TrackerSettings("http://rutor.info", priority: "torrent");

        public TrackerSettings Megapeer = new TrackerSettings("http://megapeer.vip");

        public TrackerSettings TorrentBy = new TrackerSettings("http://torrent.by", priority: "torrent");

        public TrackerSettings Kinozal = new TrackerSettings("http://kinozal.tv");

        public TrackerSettings NNMClub = new TrackerSettings("https://nnmclub.to");

        public TrackerSettings Bitru = new TrackerSettings("https://bitru.org");

        public TrackerSettings Toloka = new TrackerSettings("https://toloka.to", enable: false);

        public TrackerSettings Rutracker = new TrackerSettings("https://rutracker.net", enable: false, priority: "torrent");

        public TrackerSettings Underverse = new TrackerSettings("https://underver.se", enable: false);

        public TrackerSettings Selezen = new TrackerSettings("https://selezen.org", enable: false, priority: "torrent");

        public TrackerSettings Lostfilm = new TrackerSettings("https://www.lostfilm.tv", enable: false);

        public TrackerSettings Anilibria = new TrackerSettings("https://www.anilibria.tv");

        public TrackerSettings Animelayer = new TrackerSettings("http://animelayer.ru", enable: false);

        public TrackerSettings Anifilm = new TrackerSettings("https://anifilm.tv");


        public ProxySettings proxy = new ProxySettings();

        public List<ProxySettings> globalproxy = new List<ProxySettings>()
        {
            new ProxySettings()
            {
                pattern = "\\.onion",
                list = new List<string>() { "socks5://127.0.0.1:9050" }
            }
        };


        public List<OverrideResponse> overrideResponse = new List<OverrideResponse>()
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

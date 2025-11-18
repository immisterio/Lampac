using Lampac.Engine;
using Lampac.Engine.CRON;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared;
using Shared.Engine;
using Shared.Models.Base;
using Shared.Models.SISI.Base;
using Shared.Models.SQL;
using Shared.PlaywrightCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Lampac
{
    public class Program
    {
        #region static
        public static bool _reload = true;

        static IHost _host;

        public static List<(IPAddress prefix, int prefixLength)> cloudflare_ips = new List<(IPAddress prefix, int prefixLength)>();

        static Timer _usersTimer, _kitTimer;
        #endregion

        #region Main
        public static void Main(string[] args)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            bool IsAssemblyLoaded(AssemblyName assemblyName)
            {
                foreach (var assembly in assemblies)
                {
                    if (assembly.GetName().Name == assemblyName.Name)
                        return true;
                }

                return false;
            }

            foreach (string dllPath in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "runtimes", "references"), "*.dll"))
            {
                try
                {
                    AssemblyName assemblyName = AssemblyName.GetAssemblyName(dllPath);
                    if (!IsAssemblyLoaded(assemblyName))
                        Assembly.LoadFrom(dllPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load {dllPath}: {ex.Message}");
                }
            }


            AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
            {
                foreach (string name in new string[] { $"ru/{assemblyName.Name}", assemblyName.Name })
                {
                    string assemblyPath = Path.Combine(AppContext.BaseDirectory, "runtimes", "references", name);
                    if (File.Exists(assemblyPath))
                        return context.LoadFromAssemblyPath(assemblyPath);
                }

                return null;
            };

            Run(args);
        }
        #endregion

        #region Run
        static void Run(string[] args)
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            #region GC
            var gc = AppInit.conf.GC;
            if (gc != null && gc.enable && (gc.aggressive || AppInit.conf.multiaccess == false))
            {
                if (gc.Concurrent.HasValue)
                    AppContext.SetSwitch("System.GC.Concurrent", gc.Concurrent.Value);

                if (gc.ConserveMemory.HasValue)
                    AppContext.SetData("System.GC.ConserveMemory", gc.ConserveMemory.Value);

                if (gc.HighMemoryPercent.HasValue)
                    AppContext.SetData("System.GC.HighMemoryPercent", gc.HighMemoryPercent.Value);

                if (gc.RetainVM.HasValue)
                    AppContext.SetSwitch("System.GC.RetainVM", gc.RetainVM.Value);
            }
            #endregion

            Http.onlog += (e, log) => nws.SendLog(log, "http");

            RchClient.hub += (e, req) =>
            {
                _ = nws.SendRchRequestAsync(req.connectionId, req.rchId, req.url, req.data, req.headers, req.returnHeaders).ConfigureAwait(false);
                _ = soks.hubClients?.Client(req.connectionId)?.SendAsync("RchClient", req.rchId, req.url, req.data, req.headers, req.returnHeaders)?.ConfigureAwait(false);
            };

            string init = JsonConvert.SerializeObject(AppInit.conf, Formatting.Indented, new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            Console.WriteLine(init + "\n");
            File.WriteAllText("current.conf", JsonConvert.SerializeObject(AppInit.conf, Formatting.Indented));

            ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);
            ThreadPool.SetMinThreads(Math.Max(4096, workerThreads), Math.Max(1024, completionPortThreads));

            #region passwd
            if (!File.Exists("passwd"))
            {
                AppInit.rootPasswd = Guid.NewGuid().ToString();
                File.WriteAllText("passwd", AppInit.rootPasswd);
            }
            else
            {
                AppInit.rootPasswd = File.ReadAllText("passwd");
                AppInit.rootPasswd = Regex.Replace(AppInit.rootPasswd, "[\n\r\t ]+", "").Trim();
            }
            #endregion

            #region vers.txt
            if (!File.Exists("data/vers.txt"))
                File.WriteAllText("data/vers.txt", BaseController.appversion);

            if (!File.Exists("data/vers-minor.txt"))
                File.WriteAllText("data/vers-minor.txt", "1");
            #endregion

            #region SQL
            ExternalidsContext.Initialization();
            HybridCacheContext.Initialization();
            SisiContext.Initialization();
            ProxyLinkContext.Initialization();
            PlaywrightContext.Initialization();
            SyncUserContext.Initialization();
            #endregion

            #region migration
            if (Directory.Exists("cache/storage") || Directory.Exists("cache/bookmarks/sisi"))
            {
                Console.WriteLine("run migration");

                #region cache/storage
                if (Directory.Exists("cache/storage"))
                {
                    string sourceDir = "cache/storage";
                    string targetDir = "database/storage";

                    void CopyAll(string source, string target)
                    {
                        Directory.CreateDirectory(target);

                        foreach (string file in Directory.GetFiles(source))
                        {
                            string destFile = Path.Combine(target, Path.GetFileName(file));
                            File.Copy(file, destFile, true);
                        }

                        foreach (string dir in Directory.GetDirectories(source))
                        {
                            string destDir = Path.Combine(target, Path.GetFileName(dir));
                            CopyAll(dir, destDir);
                        }
                    }

                    CopyAll(sourceDir, targetDir);

                    Directory.Move("cache/storage", "cache/storage.bak");
                }
                #endregion

                #region cache/bookmarks/sisi
                if (Directory.Exists("cache/bookmarks/sisi"))
                {
                    using (var sqlDb = new SisiContext())
                    {
                        var existing = new HashSet<string>(
                            sqlDb.bookmarks
                                 .AsNoTracking()
                                 .Select(i => $"{i.user}:{i.uid}")
                        );

                        foreach (string folder in Directory.GetDirectories("cache/bookmarks/sisi"))
                        {
                            string folderName = Path.GetFileName(folder);

                            foreach (string file in Directory.GetFiles(folder))
                            {
                                try
                                {
                                    string md5user = folderName + Path.GetFileName(file);
                                    var bookmarks = JsonConvert.DeserializeObject<List<PlaylistItem>>(File.ReadAllText(file));

                                    if (bookmarks == null || bookmarks.Count == 0)
                                        continue;

                                    DateTime now = DateTime.UtcNow;

                                    for (int i = 0; i < bookmarks.Count; i++)
                                    {
                                        var pl = bookmarks[i];

                                        if (pl?.bookmark == null || string.IsNullOrEmpty(pl.bookmark.uid))
                                            continue;

                                        if (!existing.Add($"{md5user}:{pl.bookmark.uid}"))
                                            continue;

                                        sqlDb.bookmarks.Add(new SisiBookmarkSqlModel
                                        {
                                            user = md5user,
                                            uid = pl.bookmark.uid,
                                            created = now.AddSeconds(-i),
                                            json = JsonConvert.SerializeObject(pl),
                                            name = pl.name,
                                            model = pl.model?.name
                                        });
                                    }
                                }
                                catch { }
                            }
                        }

                        sqlDb.SaveChanges();
                    }

                    Directory.Move("cache/bookmarks/sisi", "cache/bookmarks/sisi.bak");
                }
                #endregion
            }
            #endregion

            #region Playwright
            if (AppInit.conf.chromium.enable || AppInit.conf.firefox.enable)
            {
                if (!AppInit.conf.multiaccess)
                    Environment.SetEnvironmentVariable("NODE_OPTIONS", "--max-old-space-size=256");

                ThreadPool.QueueUserWorkItem(async _ =>
                {
                    if (await PlaywrightBase.InitializationAsync())
                    {
                        if (AppInit.conf.chromium.enable)
                            _ = Chromium.CreateAsync().ConfigureAwait(false);

                        if (AppInit.conf.firefox.enable)
                            _ = Firefox.CreateAsync().ConfigureAwait(false);
                    }
                });

                Chromium.CronStart();
                Firefox.CronStart();
            }
            #endregion

            #region cloudflare_ips
            ThreadPool.QueueUserWorkItem(async _ => 
            {
                string ips = await Http.Get("https://www.cloudflare.com/ips-v4");
                if (ips == null || !ips.Contains("173.245."))
                    ips = File.Exists("data/cloudflare/ips-v4.txt") ? File.ReadAllText("data/cloudflare/ips-v4.txt") : null;

                if (ips != null)
                {
                    string ips_v6 = await Http.Get("https://www.cloudflare.com/ips-v6");
                    if (ips_v6 == null || !ips_v6.Contains("2400:cb00"))
                        ips_v6 = File.Exists("data/cloudflare/ips-v6.txt") ? File.ReadAllText("data/cloudflare/ips-v6.txt") : null;

                    if (ips_v6 != null)
                    {
                        foreach (string ip in (ips + "\n" + ips_v6).Split('\n'))
                        {
                            if (string.IsNullOrEmpty(ip) || !ip.Contains("/"))
                                continue;

                            try
                            {
                                string[] ln = ip.Split('/');
                                cloudflare_ips.Add((IPAddress.Parse(ln[0].Trim()), int.Parse(ln[1].Trim())));
                            }
                            catch { }
                        }
                    }
                }

                Console.WriteLine($"cloudflare_ips: {cloudflare_ips.Count}");
            });
            #endregion

            #region fix update.sh
            if (File.Exists("update.sh"))
            {
                var olds = new string[] 
                {
                    "02a7e97392e63b7e9e35a39ce475d6f8",
                    "6354eab8b101af90cb247fc8c977dd6b",
                    "b94b42ff158682661761a0b50a808a3b",
                    "97b0d657786b14e6a2faf7186de0556c",
                    "6b60a4d2173e99b11ecf4e792a24f598",
                    "cae6f0e79bbb2e6832922f25614d83a1",
                    "97b0d657786b14e6a2faf7186de0556c",
                    "cae6f0e79bbb2e6832922f25614d83a1",
                    "587794ca93c8d0318332858cf0e71e98",
                    "174ac2b94c5aa0e5ac086f843fd086a6",
                    "9c258d50e9eb06316efdf33de8b66dc3",
                    "bb4d6f2ba74b6a25dc3e4638c7f5282a",
                    "9607ae5805eaf5d06220298581a99beb",
                    "30078b973188c696273e10d6ef0ebbb2",
                    "92f5e2e03d2cc2697f2ee00becdb4696",
                    "b565c7e163485b8f8cc258b95f2891b6",
                    "ec6659f1f91f1f6ec0c734ff2111c7d7"
                };

                try
                {
                    if (olds.Contains(CrypTo.md5(File.ReadAllText("update.sh"))))
                    {
                        ThreadPool.QueueUserWorkItem(async _ => 
                        {
                            string new_update = await Http.Get("https://raw.githubusercontent.com/immisterio/Lampac/refs/heads/main/update.sh");
                            if (new_update != null && new_update.Contains("DEST=\"/home/lampac\""))
                                File.WriteAllText("update.sh", new_update);
                        });
                    }
                }
                catch { }
            }
            #endregion

            CacheCron.Run();
            KurwaCron.Run();
            PluginsCron.Run();
            SyncCron.Run();
            TrackersCron.Run();
            LampaCron.Run();

            _usersTimer = new Timer(UpdateUsersDb, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            _kitTimer = new Timer(UpdateKitDb, null, TimeSpan.Zero, TimeSpan.FromSeconds(Math.Max(5, AppInit.conf.kit.cacheToSeconds)));

            while (_reload)
            {
                _host = CreateHostBuilder(args).Build();
                _reload = false;
                _host.Run();
            }
        }
        #endregion


        #region Reload
        public static void Reload()
        {
            _reload = true;
            _host.StopAsync();

            AppInit.LoadModules();
        }
        #endregion

        #region CreateHostBuilder
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(op =>
                    {
                        op.AddServerHeader = false;

                        if (AppInit.conf.listen.keepalive.HasValue && AppInit.conf.listen.keepalive.Value > 0)
                            op.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(AppInit.conf.listen.keepalive.Value);

                        op.ConfigureEndpointDefaults(endpointOptions =>
                        {
                            if (AppInit.conf.listen.endpointDefaultsProtocols.HasValue)
                                endpointOptions.Protocols = AppInit.conf.listen.endpointDefaultsProtocols.Value;
                        });

                        if (string.IsNullOrEmpty(AppInit.conf.listen.sock) && string.IsNullOrEmpty(AppInit.conf.listen.ip))
                        {
                            op.Listen(IPAddress.Parse("127.0.0.1"), 9118);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(AppInit.conf.listen.sock))
                            {
                                if (File.Exists($"/var/run/{AppInit.conf.listen.sock}.sock"))
                                    File.Delete($"/var/run/{AppInit.conf.listen.sock}.sock");

                                op.ListenUnixSocket($"/var/run/{AppInit.conf.listen.sock}.sock");
                            }

                            if (!string.IsNullOrEmpty(AppInit.conf.listen.ip))
                                op.Listen(AppInit.conf.listen.ip == "any" ? IPAddress.Any : AppInit.conf.listen.ip == "broadcast" ? IPAddress.Broadcast : IPAddress.Parse(AppInit.conf.listen.ip), AppInit.conf.listen.port);
                        }
                    })
                    .UseStartup<Startup>();
                });
        #endregion


        #region UpdateUsersDb
        static bool _updateUsersDb = false;
        static string _usersKeyUpdate = string.Empty;

        static void UpdateUsersDb(object state)
        {
            if (_updateUsersDb)
                return;

            try
            {
                _updateUsersDb = true;

                if (File.Exists("users.json"))
                {
                    var lastWriteTime = File.GetLastWriteTime("users.json");

                    string keyUpdate = $"{AppInit.conf?.guid}:{AppInit.conf?.accsdb?.users?.Count ?? 0}:{lastWriteTime}";
                    if (keyUpdate == _usersKeyUpdate)
                        return;

                    foreach (var user in JsonConvert.DeserializeObject<List<AccsUser>>(File.ReadAllText("users.json")))
                    {
                        try
                        {
                            var find = AppInit.conf.accsdb.findUser(user.id ?? user.ids?.First());
                            if (find != null)
                            {
                                find.id = user.id;
                                find.ids = user.ids;
                                find.group = user.group;
                                find.IsPasswd = user.IsPasswd;
                                find.expires = user.expires;
                                find.ban = user.ban;
                                find.ban_msg = user.ban_msg;
                                find.comment = user.comment;
                                find.@params = user.@params;
                            }
                            else
                            {
                                AppInit.conf.accsdb.users.Add(user);
                            }
                        }
                        catch { }
                    }

                    _usersKeyUpdate = keyUpdate;
                }
            }
            catch { }
            finally
            {
                _updateUsersDb = false;
            }
        }
        #endregion

        #region UpdateKitDb
        static bool _updateKitDb = false;

        async static void UpdateKitDb(object state)
        {
            if (_updateKitDb)
                return;

            try
            {
                _updateKitDb = true;

                if (AppInit.conf.kit.enable && AppInit.conf.kit.IsAllUsersPath && !string.IsNullOrEmpty(AppInit.conf.kit.path))
                {
                    var users = await Http.Get<Dictionary<string, JObject>>(AppInit.conf.kit.path);
                    if (users != null)
                        AppInit.conf.kit.allUsers = users;
                }
            }
            catch { }
            finally
            {
                _updateKitDb = false;
            }
        }
        #endregion
    }
}

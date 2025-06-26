using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Engine.CRON;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using Shared.Model.Base;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lampac
{
    public class Program
    {
        static bool _reload = true;

        static IHost _host;

        public static List<(IPAddress prefix, int prefixLength)> cloudflare_ips = new List<(IPAddress prefix, int prefixLength)>();


        public static void Main(string[] args)
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            HttpClient.onlog += (e, log) => soks.SendLog(log, "http");
            RchClient.hub += (e, req) => soks.hubClients?.Client(req.connectionId)?.SendAsync("RchClient", req.rchId, req.url, req.data, req.headers, req.returnHeaders)?.ConfigureAwait(false);

            string init = JsonConvert.SerializeObject(AppInit.conf, Formatting.Indented, new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            Console.WriteLine(init + "\n");
            File.WriteAllText("current.conf", JsonConvert.SerializeObject(AppInit.conf, Formatting.Indented));

            ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);
            ThreadPool.SetMinThreads(Math.Max(4096, workerThreads), Math.Max(1024, completionPortThreads));

            #region Playwright
            if (AppInit.conf.chromium.enable || AppInit.conf.firefox.enable)
            {
                if (!AppInit.conf.multiaccess)
                    Environment.SetEnvironmentVariable("NODE_OPTIONS", "--max-old-space-size=128");

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

                if (AppInit.conf.chromium.enable)
                {
                    ThreadPool.QueueUserWorkItem(async _ => await Chromium.CloseLifetimeContext().ConfigureAwait(false));
                    ThreadPool.QueueUserWorkItem(async _ => await Chromium.Browser_Disconnected().ConfigureAwait(false));
                }

                if (AppInit.conf.firefox.enable)
                    ThreadPool.QueueUserWorkItem(async _ => await Firefox.CloseLifetimeContext().ConfigureAwait(false));
            }
            #endregion

            #region passwd
            if (!File.Exists("passwd"))
            {
                AppInit.rootPasswd = Guid.NewGuid().ToString();
                File.WriteAllText("passwd", AppInit.rootPasswd);
            }
            else
            {
                AppInit.rootPasswd = File.ReadAllText("passwd");
            }
            #endregion

            if (!File.Exists("vers.txt"))
                File.WriteAllText("vers.txt", BaseController.appversion);

            if (!File.Exists("vers-minor.txt"))
                File.WriteAllText("vers-minor.txt", "1");

            ThreadPool.QueueUserWorkItem(async _ => await SyncCron.Run().ConfigureAwait(false));
            ThreadPool.QueueUserWorkItem(async _ => await LampaCron.Run().ConfigureAwait(false));
            ThreadPool.QueueUserWorkItem(async _ => await CacheCron.Run().ConfigureAwait(false));
            ThreadPool.QueueUserWorkItem(async _ => await TrackersCron.Run().ConfigureAwait(false));
            ThreadPool.QueueUserWorkItem(async _ => await ProxyLink.Cron().ConfigureAwait(false));
            ThreadPool.QueueUserWorkItem(async _ => await PluginsCron.Run().ConfigureAwait(false));
            ThreadPool.QueueUserWorkItem(async _ => await KurwaCron.Run().ConfigureAwait(false));

            #region kitAllUsers
            ThreadPool.QueueUserWorkItem(async _ => 
            {
                while (true)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, AppInit.conf.kit.cacheToSeconds))).ConfigureAwait(false);

                        if (AppInit.conf.kit.enable && AppInit.conf.kit.IsAllUsersPath && !string.IsNullOrEmpty(AppInit.conf.kit.path))
                        {
                            var users = await HttpClient.Get<Dictionary<string, JObject>>(AppInit.conf.kit.path).ConfigureAwait(false);
                            if (users != null)
                                AppInit.conf.kit.allUsers = users;
                        }
                    }
                    catch { }
                }
            });
            #endregion

            #region cloudflare_ips
            ThreadPool.QueueUserWorkItem(async _ => 
            {
                string ips = await HttpClient.Get("https://www.cloudflare.com/ips-v4").ConfigureAwait(false);
                if (ips == null || !ips.Contains("173.245."))
                    ips = File.Exists("data/cloudflare/ips-v4.txt") ? File.ReadAllText("data/cloudflare/ips-v4.txt") : null;

                if (ips != null)
                {
                    string ips_v6 = await HttpClient.Get("https://www.cloudflare.com/ips-v6").ConfigureAwait(false);
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

            #region users.json
            ThreadPool.QueueUserWorkItem(async _ =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

                    try
                    {
                        if (File.Exists("users.json"))
                        {
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
                        }
                    }
                    catch { }
                }
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
                            string new_update = await HttpClient.Get("https://raw.githubusercontent.com/immisterio/Lampac/refs/heads/main/update.sh").ConfigureAwait(false);
                            if (new_update != null && new_update.Contains("DEST=\"/home/lampac\""))
                                File.WriteAllText("update.sh", new_update);
                        });
                    }
                }
                catch { }
            }
            #endregion

            while (_reload)
            {
                _reload = false;
                _host = CreateHostBuilder(args).Build();
                _host.Run();
            }
        }

        public static void Reload()
        {
            _reload = true;

            AppInit.LoadModules();
            _host.StopAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(op => 
                    {
                        if (string.IsNullOrEmpty(AppInit.conf.listen_sock) && string.IsNullOrEmpty(AppInit.conf.listenip))
                        {
                            op.Listen(IPAddress.Parse("127.0.0.1"), 9118);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(AppInit.conf.listen_sock))
                            {
                                if (File.Exists($"/var/run/{AppInit.conf.listen_sock}.sock"))
                                    File.Delete($"/var/run/{AppInit.conf.listen_sock}.sock");

                                op.ListenUnixSocket($"/var/run/{AppInit.conf.listen_sock}.sock");
                            }

                            if (!string.IsNullOrEmpty(AppInit.conf.listenip))
                                op.Listen(AppInit.conf.listenip == "any" ? IPAddress.Any : AppInit.conf.listenip == "broadcast" ? IPAddress.Broadcast : IPAddress.Parse(AppInit.conf.listenip), AppInit.conf.listenport);
                        }
                    })
                    .UseStartup<Startup>();
                });
    }
}

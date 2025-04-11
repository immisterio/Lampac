using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Globalization;
using System.Text;
using System.Threading;
using Lampac.Engine.CRON;
using Lampac.Engine.CORE;
using System;
using System.IO;
using Newtonsoft.Json;
using Shared.Engine;
using Lampac.Engine;
using Microsoft.AspNetCore.SignalR;
using System.Linq;

namespace Lampac
{
    public class Program
    {
        static bool _reload = true;

        static IHost _host;

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
            
            if (AppInit.conf.mikrotik) 
            {
                #region GC
                {
                    var timer = new System.Timers.Timer(1000 * 60);
                    timer.Elapsed += (sender, e) =>
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    };
                    timer.AutoReset = true;
                    timer.Enabled = true;
                }
                #endregion
            }
            else
            {
                ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);
                ThreadPool.SetMinThreads(Math.Max(4096, workerThreads), Math.Max(1024, completionPortThreads));
            }

            #region Playwright
            if (AppInit.conf.chromium.enable || AppInit.conf.firefox.enable)
            {
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

                ThreadPool.QueueUserWorkItem(async _ => await Chromium.CloseLifetimeContext());
                ThreadPool.QueueUserWorkItem(async _ => await Firefox.CloseLifetimeContext());
            }
            #endregion

            if (!File.Exists("passwd"))
                File.WriteAllText("passwd", Guid.NewGuid().ToString());

            if (!File.Exists("vers.txt"))
                File.WriteAllText("vers.txt", BaseController.appversion);

            if (!File.Exists("vers-minor.txt"))
                File.WriteAllText("vers-minor.txt", "1");

            ThreadPool.QueueUserWorkItem(async _ => await SyncCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await LampaCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await CacheCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await TrackersCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await ProxyLink.Cron());
            ThreadPool.QueueUserWorkItem(async _ => await PluginsCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await KurwaCron.Run());

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
                            string new_update = await HttpClient.Get("https://raw.githubusercontent.com/immisterio/Lampac/refs/heads/main/update.sh");
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
                        if (!string.IsNullOrEmpty(AppInit.conf.listen_sock))
                            op.ListenUnixSocket($"/var/run/{AppInit.conf.listen_sock}.sock");
                        else
                            op.Listen(AppInit.conf.listenip == "any" ? IPAddress.Any : AppInit.conf.listenip == "broadcast" ? IPAddress.Broadcast : IPAddress.Parse(AppInit.conf.listenip), AppInit.conf.listenport);
                    })
                    .UseStartup<Startup>();
                });
    }
}

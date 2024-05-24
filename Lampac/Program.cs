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
using PuppeteerSharp;
using Shared.Engine;
using Lampac.Engine;
using Microsoft.AspNetCore.SignalR;

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
            RchClient.hub += (e, req) => soks.hubClients?.Client(req.connectionId)?.SendAsync("RchClient", req.rchId, req.url, req.data);

            Console.WriteLine(JsonConvert.SerializeObject(AppInit.conf, Formatting.Indented, new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            }) + "\n");

            if (AppInit.conf.multiaccess)
            {
                ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);
                ThreadPool.SetMinThreads(Math.Max(200, workerThreads), Math.Max(20, completionPortThreads));
            }

            #region puppeteer
            try
            {
                if (AppInit.conf.puppeteer.enable)
                {
                    ThreadPool.QueueUserWorkItem(async _ =>
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(AppInit.conf.puppeteer.executablePath))
                                await new BrowserFetcher().DownloadAsync();

                            if (PuppeteerTo.IsKeepOpen)
                                PuppeteerTo.LaunchKeepOpen();
                        }
                        catch (Exception ex) { Console.WriteLine(ex); }
                    });
                }
            }
            catch { }
            #endregion

            if (!File.Exists("passwd"))
                File.WriteAllText("passwd", Guid.NewGuid().ToString());

            if (!File.Exists("vers.txt"))
                File.WriteAllText("vers.txt", BaseController.appversion);

            if (!File.Exists("vers-minor.txt"))
                File.WriteAllText("vers-minor.txt", BaseController.minorversion);

            ThreadPool.QueueUserWorkItem(async _ => await LampaCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await CacheCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await TrackersCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await ProxyLink.Cron());
            ThreadPool.QueueUserWorkItem(async _ => await PluginsCron.Run());

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

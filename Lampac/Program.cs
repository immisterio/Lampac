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
using DnsClient;
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
            RchClient.hub += (e, req) => soks.hubClients?.Client(req.connectionId)?.SendAsync("RchClient", req.rchId, req.url, req.data, req.headers, req.returnHeaders);

            string init = JsonConvert.SerializeObject(AppInit.conf, Formatting.Indented, new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            Console.WriteLine(init + "\n");
            File.WriteAllText("current.conf", init);
            
            if (!AppInit.conf.mikrotik) 
            {
                ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);
                ThreadPool.SetMinThreads(Math.Max(4096, workerThreads), Math.Max(1024, completionPortThreads));
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
                File.WriteAllText("vers-minor.txt", "1");

            ThreadPool.QueueUserWorkItem(async _ => await SyncCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await LampaCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await CacheCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await TrackersCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await ProxyLink.Cron());
            ThreadPool.QueueUserWorkItem(async _ => await PluginsCron.Run());

            #region tmdb proxy
            var tmdb = AppInit.conf.serverproxy.tmdb;
            if (!tmdb.useproxy && (string.IsNullOrWhiteSpace(tmdb.API_IP) || string.IsNullOrWhiteSpace(tmdb.IMG_IP)))
            {
                ThreadPool.QueueUserWorkItem(async _ =>
                {
                    var lookup = new LookupClient(IPAddress.Parse(tmdb.DNS ?? "9.9.9.9"));

                    #region api.themoviedb.org
                    if (string.IsNullOrWhiteSpace(tmdb.API_IP))
                    {
                        string uri = "https://api.themoviedb.org/3/movie/1079091?api_key=4ef0d7355d9ffb5151e987764708ce96&append_to_response=content_ratings,release_dates,keywords,alternative_titles&language=ru";
                        string json = await HttpClient.Get(uri, timeoutSeconds: 10);
                        if (json == null || !json.Contains("1079091"))
                        {
                            var result = await lookup.QueryAsync("api.themoviedb.org", QueryType.A);
                            tmdb.API_IP = result?.Answers?.ARecords()?.FirstOrDefault()?.Address?.ToString();
                        }
                    }
                    #endregion

                    #region image.tmdb.org
                    if (string.IsNullOrWhiteSpace(tmdb.IMG_IP))
                    {
                        byte[] img = await HttpClient.Download("https://image.tmdb.org/t/p/w300/54U26SA33pxxJ2lf5mRxWeqRTLu.jpg", timeoutSeconds: 10);
                        if (img == null || img.Length != 13160)
                        {
                            var result = await lookup.QueryAsync("image.tmdb.org", QueryType.A);
                            tmdb.API_IP = result?.Answers?.ARecords()?.FirstOrDefault()?.Address?.ToString();
                        }
                    }
                    #endregion
                });
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

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Globalization;
using System.Text;
using System.Threading;
using Lampac.Engine.CRON;
using Lampac.Engine.CORE;

namespace Lampac
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            ThreadPool.QueueUserWorkItem(async _ => await LampaCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await CacheCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await TrackersCron.Run());
            ThreadPool.QueueUserWorkItem(async _ => await ProxyLink.Cron());
            ThreadPool.QueueUserWorkItem(async _ => await PluginsCron.Run());

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(op => op.Listen(AppInit.conf.listenip == "any" ? IPAddress.Any : AppInit.conf.listenip == "broadcast" ? IPAddress.Broadcast : IPAddress.Parse(AppInit.conf.listenip), AppInit.conf.listenport))
                    .UseStartup<Startup>();
                });
    }
}

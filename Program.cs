using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Globalization;
using System.Text;
using System.Threading;
using Lampac.Engine.CRON;

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

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(op => op.Listen(IPAddress.Any, AppInit.conf.listenport))
                    .UseStartup<Startup>();
                });
    }
}

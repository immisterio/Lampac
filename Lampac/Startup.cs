using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Text.Json.Serialization;
using Lampac.Engine.Middlewares;
using Lampac.Engine.CORE;
using System.Net;
using System;
using Lampac.Engine;
using PuppeteerSharp;

namespace Lampac
{
    public class Startup
    {
        #region Startup
        public IConfiguration Configuration { get; }

        public static IMemoryCache memoryCache { get; private set; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        #endregion

        #region ConfigureServices
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddResponseCompression(options =>
            {
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/vnd.apple.mpegurl", "image/svg+xml" });
            });

            services.AddSignalR();

            #region mvcBuilder
            IMvcBuilder mvcBuilder = services.AddControllersWithViews();

            if (AppInit.modules != null)
            {
                foreach (var mod in AppInit.modules)
                {
                    try
                    {
                        Console.WriteLine("load module: " + mod.dll);
                        mvcBuilder.AddApplicationPart(mod.assembly);
                    }
                    catch (Exception ex) { Console.WriteLine(ex.Message + "\n"); }
                }

                Console.WriteLine();
            }

            mvcBuilder.AddJsonOptions(options => {
                //options.JsonSerializerOptions.IgnoreNullValues = true;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
            });
            #endregion
        }
        #endregion


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IMemoryCache memory)
        {
            memoryCache = memory;
            Shared.Startup.Configure(app, memory);

            app.UseDeveloperExceptionPage();

            HttpClient.onlog += (e, log) => { _= soks.Send(log, "http"); };

            #region UseForwardedHeaders
            var forwarded = new ForwardedHeadersOptions
            {
                ForwardLimit = null,
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };

            if (AppInit.conf.real_ip_cf)
            {
                string ips = HttpClient.Get("https://www.cloudflare.com/ips-v4", timeoutSeconds: 10).Result;
                if (ips != null)
                {
                    forwarded.ForwardedForHeaderName = "CF-Connecting-IP";
                    foreach (string line in ips.Split('\n'))
                    {
                        if (string.IsNullOrEmpty(line) || !line.Contains("/"))
                            continue;

                        string[] ln = line.Split('/');
                        forwarded.KnownNetworks.Add(new IPNetwork(IPAddress.Parse(ln[0]), int.Parse(ln[1])));
                    }
                }
            }

            if (AppInit.conf.KnownProxies != null && AppInit.conf.KnownProxies.Count > 0)
            {
                foreach (var k in AppInit.conf.KnownProxies)
                    forwarded.KnownNetworks.Add(new IPNetwork(IPAddress.Parse(k.ip), k.prefixLength));
            }

            app.UseForwardedHeaders(forwarded);
            #endregion

            #region Puppeteer
            new BrowserFetcher().DownloadAsync().Wait();

            AppInit.browser = Puppeteer.LaunchAsync(new LaunchOptions()
            {
                Headless = true, /*false*/
                IgnoreHTTPSErrors = true,
                Args = new string[] { "--no-sandbox --disable-gpu" },
                Timeout = 10_000
            }).Result;
            #endregion

            app.UseRouting();
            app.UseResponseCompression();
            app.UseModHeaders();
            app.UseStaticFiles();
            app.UseAccsdb();
            app.UseOverrideResponse();
            app.UseProxyIMG();
            app.UseProxyAPI();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<soks>("/ws");
                endpoints.MapControllers();
            });
        }
    }
}

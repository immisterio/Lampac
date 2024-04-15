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
using Shared.Engine;
using Shared.Engine.CORE;
using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Microsoft.AspNetCore.SignalR;

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
            #region IHttpClientFactory
            services.AddHttpClient("proxy").ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new System.Net.Http.HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    AllowAutoRedirect = false
                };

                handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
                return handler;
            });

            services.AddHttpClient("base").ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new System.Net.Http.HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    AllowAutoRedirect = true
                };

                handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
                return handler;
            });

            services.RemoveAll<IHttpMessageHandlerBuilderFilter>();
            #endregion

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


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IMemoryCache memory, System.Net.Http.IHttpClientFactory httpClientFactory)
        {
            memoryCache = memory;
            Shared.Startup.Configure(app, memory);
            HybridCache.Configure(memory);
            ProxyManager.Configure(memory);
            HttpClient.httpClientFactory = httpClientFactory;
            bool manifestload = File.Exists("module/manifest.json");

            if (manifestload)
            {
                HttpClient.onlog += (e, log) => soks.SendLog(log, "http");
                RchClient.hub += (e, req) => soks.hubClients?.Client(req.connectionId)?.SendAsync("RchClient", req.rchId, req.url, req.data);
            }

            if (!File.Exists("passwd"))
                File.WriteAllText("passwd", Guid.NewGuid().ToString());

            try
            {
                if (manifestload && AppInit.conf.puppeteer.enable)
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

            if (manifestload)
            {
                Console.WriteLine(JsonConvert.SerializeObject(AppInit.conf, Formatting.Indented, new JsonSerializerSettings()
                {
                    //DefaultValueHandling = DefaultValueHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore
                }));

                ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);

                if (AppInit.conf.multiaccess)
                    ThreadPool.SetMinThreads(Math.Max(400, workerThreads), Math.Max(200, completionPortThreads));
                else
                    ThreadPool.SetMinThreads(Math.Max(50, workerThreads), Math.Max(20, completionPortThreads));
            }

            app.UseDeveloperExceptionPage();

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

            app.UseRouting();
            app.UseResponseCompression();
            app.UseModHeaders();
            app.UseStaticFiles();
            app.UseAccsdb();
            app.UseOverrideResponse();
            app.UseProxyIMG();
            app.UseProxyAPI();
            app.UseModule();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<soks>("/ws");
                endpoints.MapControllers();
            });
        }
    }
}

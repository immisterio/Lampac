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
using Newtonsoft.Json.Linq;
using System.Text;
using System.Net;
using System.IO;

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

            #region mvcBuilder
            IMvcBuilder mvcBuilder = services.AddControllersWithViews();

            if (AppInit.modules != null)
            {
                foreach (var mod in AppInit.modules)
                    mvcBuilder.AddApplicationPart(mod.assembly);
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

            #region UseForwardedHeaders
            var forwarded = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };

            if (AppInit.conf.KnownProxies != null && AppInit.conf.KnownProxies.Count > 0)
            {
                foreach (var k in AppInit.conf.KnownProxies)
                    forwarded.KnownNetworks.Add(new IPNetwork(IPAddress.Parse(k.ip), k.prefixLength));
            }

            app.UseForwardedHeaders(forwarded);
            #endregion

            #region Update KinoPub device
            if (AppInit.conf.KinoPub.enable && !string.IsNullOrWhiteSpace(AppInit.conf.KinoPub.token))
            {
                try
                {
                    var root = HttpClient.Get<JObject>($"{AppInit.conf.KinoPub.apihost}/v1/device/info?access_token={AppInit.conf.KinoPub.token}", timeoutSeconds: 5).Result;
                    if (root != null && root.ContainsKey("device"))
                    {
                        long? device_id = root.Value<JObject>("device")?.Value<long>("id");
                        if (device_id > 0)
                        {
                            string data = "{\"supportSsl\": " + AppInit.conf.KinoPub.ssl.ToString().ToLower() + ", \"support4k\": " + AppInit.conf.KinoPub.uhd.ToString().ToLower() + ", \"supportHevc\": " + AppInit.conf.KinoPub.hevc.ToString().ToLower() + ", \"supportHdr\": " + AppInit.conf.KinoPub.hdr.ToString().ToLower() + "}";
                            _= HttpClient.Post($"{AppInit.conf.KinoPub.apihost}/v1/device/{device_id}/settings?access_token={AppInit.conf.KinoPub.token}", new System.Net.Http.StringContent(data, Encoding.UTF8, "application/json"));
                        }
                    }
                }
                catch { }
            }
            #endregion

            #region fix 
            try
            {
                string json = File.ReadAllText("Lampac.runtimeconfig.json");
                if (json != null && json.Contains(": 309715200,"))
                    File.WriteAllText("Lampac.runtimeconfig.json", json.Replace(": 309715200,", ": 409715200,"));
            }
            catch { }
            #endregion

            //AppInit.conf.Toloka.login = new Models.JAC.LoginSettings() { u = "user", p = "passwd" };
            //System.IO.File.WriteAllText("example.conf", Newtonsoft.Json.JsonConvert.SerializeObject(AppInit.conf, Newtonsoft.Json.Formatting.Indented));

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
                endpoints.MapControllers();
            });
        }
    }
}

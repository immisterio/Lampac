using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Text.Json.Serialization;
using Lampac.Engine.Middlewares;

namespace Lampac
{
    public class Startup
    {
        #region Startup
        public IConfiguration Configuration { get; }

        public static IMemoryCache memoryCache { get; private set; }

        public static IServiceProvider ApplicationServices { get; private set; }

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

            services.AddControllersWithViews().AddJsonOptions(options => {
                //options.JsonSerializerOptions.IgnoreNullValues = true;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
            });
        }
        #endregion


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IMemoryCache memory)
        {
            memoryCache = memory;

            ApplicationServices = app.ApplicationServices;
            app.UseDeveloperExceptionPage();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            //AppInit.conf.Toloka.login = new Models.JAC.LoginSettings() { u = "user", p = "passwd" };
            //System.IO.File.WriteAllText("example.conf", Newtonsoft.Json.JsonConvert.SerializeObject(AppInit.conf, Newtonsoft.Json.Formatting.Indented));

            app.UseRouting();
            app.UseResponseCompression();
            app.UseModHeaders();
            app.UseProxyIMG();
            app.UseProxyAPI();
            app.UseStaticFiles();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}

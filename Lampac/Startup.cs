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
using Shared.Engine.CORE;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Lampac.Models.Module;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyModel;

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

            if (AppInit.conf.compression)
            {
                services.AddResponseCompression(options =>
                {
                    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/vnd.apple.mpegurl", "image/svg+xml" });
                });
            }

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
            }

            if (File.Exists("module/manifest.json"))
            {
                var jss = new JsonSerializerSettings
                {
                    Error = (se, ev) =>
                    {
                        ev.ErrorContext.Handled = true;
                        Console.WriteLine("module/manifest.json - " + ev.ErrorContext.Error + "\n\n");
                    }
                };

                var mods = JsonConvert.DeserializeObject<List<RootModule>>(File.ReadAllText("module/manifest.json"), jss);
                if (mods == null)
                    return;

                List<PortableExecutableReference> references = null;

                foreach (var mod in mods)
                {
                    if (!mod.enable || mod.dll.EndsWith(".dll"))
                        continue;

                    string path = File.Exists(mod.dll) ? mod.dll : $"{Environment.CurrentDirectory}/module/{mod.dll}";
                    if (Directory.Exists(path))
                    {
                        var syntaxTree = new List<SyntaxTree>();

                        foreach (string file in Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories))
                            syntaxTree.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(file)));

                        if (references == null)
                        {
                            var dependencyContext = DependencyContext.Default;
                            var assemblies = dependencyContext.RuntimeLibraries
                                .SelectMany(library => library.GetDefaultAssemblyNames(dependencyContext))
                                .Select(Assembly.Load)
                                .ToList();

                            references = assemblies.Select(assembly => MetadataReference.CreateFromFile(assembly.Location)).ToList();
                        }

                        CSharpCompilation compilation = CSharpCompilation.Create(Path.GetFileName(mod.dll), syntaxTree, references: references, options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                        using (var ms = new MemoryStream())
                        {
                            var result = compilation.Emit(ms);

                            if (!result.Success)
                            {
                                foreach (var diagnostic in result.Diagnostics)
                                    Console.WriteLine(diagnostic);
                            }
                            else
                            {
                                ms.Seek(0, SeekOrigin.Begin);
                                mod.assembly = Assembly.Load(ms.ToArray());

                                if (mod.initspace != null && mod.assembly.GetType(mod.initspace) is Type t && t.GetMethod("loaded") is MethodInfo m)
                                    m.Invoke(null, new object[] { });

                                Console.WriteLine("load module: " + mod.dll);
                                AppInit.modules.Add(mod);
                                mvcBuilder.AddApplicationPart(mod.assembly);
                            }
                        }
                    }
                }
            }

            Console.WriteLine();

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

            if (AppInit.conf.compression)
                app.UseResponseCompression();

            app.UseModHeaders();
            app.UseStaticFiles();
            app.UseAccsdb();
            app.UseOverrideResponse();
            app.UseProxyIMG();
            app.UseProxyAPI();
            app.UseModule();
            app.UseCache();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<soks>("/ws");
                endpoints.MapControllers();
            });
        }
    }
}

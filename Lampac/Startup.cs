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
using Microsoft.Extensions.Hosting;
using Shared.Engine;
using Shared.Models.Module;

namespace Lampac
{
    public class Startup
    {
        #region Startup
        public IConfiguration Configuration { get; }

        public static IServiceCollection serviceCollection { get; private set; }

        public static IMemoryCache memoryCache { get; private set; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        #endregion

        #region ConfigureServices
        public void ConfigureServices(IServiceCollection services)
        {
            serviceCollection = services;

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

            services.AddSignalR(o =>
            {
                o.EnableDetailedErrors = true;
                o.MaximumParallelInvocationsPerClient = 5;
            });

            IMvcBuilder mvcBuilder = services.AddControllersWithViews();

            mvcBuilder.AddJsonOptions(options => {
                //options.JsonSerializerOptions.IgnoreNullValues = true;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
            });

            #region module/references
            string referencesPath = Path.Combine(Environment.CurrentDirectory, "module", "references");
            if (Directory.Exists(referencesPath))
            {
                var current = AppDomain.CurrentDomain.GetAssemblies();
                foreach (string dllFile in Directory.GetFiles(referencesPath, "*.dll", SearchOption.AllDirectories))
                {
                    try
                    {
                        string loadedName = Path.GetFileNameWithoutExtension(dllFile);
                        if (current.Any(a => string.Equals(a.GetName().Name, loadedName, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        Assembly loadedAssembly = Assembly.LoadFrom(dllFile);
                        mvcBuilder.AddApplicationPart(loadedAssembly);
                        Console.WriteLine($"load reference: {Path.GetFileName(dllFile)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load reference {dllFile}: {ex.Message}");
                    }
                }
            }
            #endregion

            #region compilation modules
            if (AppInit.modules != null)
            {
                // mod.dll
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

            // сборка dll из source
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

                #region CompilationMod
                List<PortableExecutableReference> references = null;

                void CompilationMod(RootModule mod)
                {
                    if (!mod.enable || AppInit.modules.FirstOrDefault(i => i.dll == mod.dll) != null)
                        return;

                    if (mod.dll.EndsWith(".dll"))
                    {
                        try
                        {
                            mod.assembly = Assembly.LoadFrom(mod.dll);

                            AppInit.modules.Add(mod);
                            mvcBuilder.AddApplicationPart(mod.assembly);
                            Console.WriteLine($"load module: {Path.GetFileName(mod.dll)}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to load reference {mod.dll}: {ex.Message}");
                        }

                        return;
                    }

                    string path = Directory.Exists(mod.dll) ? mod.dll : $"{Environment.CurrentDirectory}/module/{mod.dll}";
                    if (Directory.Exists(path))
                    {
                        var syntaxTree = new List<SyntaxTree>();

                        foreach (string file in Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories))
                        {
                            if (file.Contains("/obj/"))
                                continue;

                            syntaxTree.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(file)));
                        }

                        if (references == null)
                        {
                            var dependencyContext = DependencyContext.Default;
                            var assemblies = dependencyContext.RuntimeLibraries
                                .SelectMany(library => library.GetDefaultAssemblyNames(dependencyContext))
                                .Select(Assembly.Load)
                                .ToList();

                            references = assemblies.Select(assembly => MetadataReference.CreateFromFile(assembly.Location)).ToList();
                        }

                        if (mod.references != null)
                        {
                            foreach (string refns in mod.references)
                            {
                                string dlrns = Path.Combine(Environment.CurrentDirectory, "module", mod.dll, refns);
                                if (File.Exists(dlrns) && references.FirstOrDefault(a => Path.GetFileName(a.FilePath) == refns) == null)
                                {
                                    var assembly = Assembly.LoadFrom(dlrns);
                                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                                }
                            }
                        }

                        CSharpCompilation compilation = CSharpCompilation.Create(Path.GetFileName(mod.dll), syntaxTree, references: references, options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                        using (var ms = new MemoryStream())
                        {
                            var result = compilation.Emit(ms);

                            if (!result.Success)
                            {
                                Console.WriteLine($"\ncompilation error: {mod.dll}");
                                foreach (var diagnostic in result.Diagnostics)
                                {
                                    if (diagnostic.Severity == DiagnosticSeverity.Error)
                                        Console.WriteLine(diagnostic);
                                }
                                Console.WriteLine();
                            }
                            else
                            {
                                ms.Seek(0, SeekOrigin.Begin);
                                mod.assembly = Assembly.Load(ms.ToArray());

                                Console.WriteLine("compilation module: " + mod.dll);
                                mod.index = mod.index != 0 ? mod.index : (100 + AppInit.modules.Count);
                                AppInit.modules.Add(mod);
                                mvcBuilder.AddApplicationPart(mod.assembly);
                            }
                        }
                    }
                }
                #endregion

                foreach (var mod in mods)
                    CompilationMod(mod);

                foreach (string folderMod in Directory.GetDirectories("module/"))
                {
                    string manifest = $"{Environment.CurrentDirectory}/{folderMod}/manifest.json";
                    if (!File.Exists(manifest))
                        continue;

                    var mod = JsonConvert.DeserializeObject<RootModule>(File.ReadAllText(manifest), jss);
                    if (mod != null)
                    {
                        if (mod.dll == null)
                            mod.dll = folderMod.Split("/")[1];
                        else if (mod.dll.EndsWith(".dll"))
                            mod.dll = Path.Combine(folderMod, mod.dll);

                        CompilationMod(mod);
                    }
                }
            }

            if (AppInit.modules != null)
                AppInit.modules = AppInit.modules.OrderBy(i => i.index).ToList();

            Console.WriteLine();
            #endregion
        }
        #endregion


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IMemoryCache memory, System.Net.Http.IHttpClientFactory httpClientFactory, IHostApplicationLifetime applicationLifetime)
        {
            memoryCache = memory;
            Shared.Startup.Configure(app, memory);
            HybridCache.Configure(memory);
            ProxyManager.Configure(memory);
            HttpClient.httpClientFactory = httpClientFactory;

            #region modules loaded
            if (AppInit.modules != null)
            {
                foreach (var mod in AppInit.modules)
                {
                    try
                    {
                        if (mod.dll == "DLNA.dll")
                            mod.initspace = "DLNA.ModInit";

                        if (mod.initspace != null && mod.assembly.GetType(mod.NamespacePath(mod.initspace)) is Type t && t.GetMethod("loaded") is MethodInfo m)
                        {
                            if (mod.version >= 2)
                            {
                                m.Invoke(null, new object[] { new InitspaceModel()
                                {
                                    path = $"module/{mod.dll}", soks = new soks(), memoryCache = memoryCache, configuration = Configuration, services = serviceCollection, app = app
                                }});
                            }
                            else
                                m.Invoke(null, new object[] { });
                        }
                    }
                    catch (Exception ex) { Console.WriteLine($"Module {mod.NamespacePath(mod.initspace)}: {ex.Message}\n\n"); }
                }
            }
            #endregion

            app.UseDeveloperExceptionPage();
            applicationLifetime.ApplicationStopping.Register(OnShutdown);

            #region UseForwardedHeaders
            var forwarded = new ForwardedHeadersOptions
            {
                ForwardLimit = null,
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };

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
            app.UseOverrideResponse(first: true);
            app.UseStaticFiles();
            app.UseRequestInfo();
            app.UseAccsdb();
            app.UseOverrideResponse(first: false);
            app.UseProxyIMG();
            app.UseProxyAPI();

            if (AppInit.modules != null && AppInit.modules.FirstOrDefault(i => i.middlewares != null) != null)
                app.UseModule();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<soks>("/ws");
                endpoints.MapControllers();
            });
        }


        private void OnShutdown()
        {
            Chromium.FullDispose();
            Firefox.FullDispose();
        }
    }
}

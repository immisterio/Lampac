using Lampac.Engine;
using Lampac.Engine.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Newtonsoft.Json;
using Shared;
using Shared.Engine;
using Shared.Models.Module;
using Shared.Models.Module.Entrys;
using Shared.Models.SQL;
using Shared.PlaywrightCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Lampac
{
    public class Startup
    {
        #region Startup
        static IApplicationBuilder _app = null;

        public static bool IsShutdown { get; private set; }

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
            services.AddHttpClient("proxy").ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                SslOptions = { RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true },
                PooledConnectionLifetime = TimeSpan.FromMinutes(30),
                UseCookies = false
            });

            services.AddHttpClient("proxyRedirect").ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.None,
                SslOptions = { RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true },
                PooledConnectionLifetime = TimeSpan.FromMinutes(30),
                UseCookies = false
            });

            services.AddHttpClient("proxyimg").ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.None,
                SslOptions = { RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true },
                PooledConnectionLifetime = TimeSpan.FromMinutes(30),
                UseCookies = false
            });

            services.AddHttpClient("base").ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
                SslOptions = { RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true },
                PooledConnectionLifetime = TimeSpan.FromMinutes(30),
                UseCookies = false
            });

            services.AddHttpClient("baseNoRedirect").ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
                SslOptions = { RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true },
                PooledConnectionLifetime = TimeSpan.FromMinutes(30),
                UseCookies = false
            });

            services.AddHttpClient("http2", client =>
            {
                client.DefaultRequestVersion = HttpVersion.Version20;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
                SslOptions = { RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true },
                PooledConnectionLifetime = TimeSpan.FromMinutes(30),
                EnableMultipleHttp2Connections = true,
                UseCookies = false
            });

            services.AddHttpClient("http2proxyimg", client =>
            {
                client.DefaultRequestVersion = HttpVersion.Version20;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.None,
                SslOptions = { RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true },
                PooledConnectionLifetime = TimeSpan.FromMinutes(30),
                EnableMultipleHttp2Connections = true,
                UseCookies = false
            });

            services.AddHttpClient("http2NoRedirect", client =>
            {
                client.DefaultRequestVersion = HttpVersion.Version20;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
                SslOptions = { RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true },
                PooledConnectionLifetime = TimeSpan.FromMinutes(30),
                EnableMultipleHttp2Connections = true,
                UseCookies = false
            });

            services.AddHttpClient("http3", client =>
            {
                client.DefaultRequestVersion = HttpVersion.Version30;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
                SslOptions = { RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true },
                PooledConnectionLifetime = TimeSpan.FromMinutes(30),
                EnableMultipleHttp2Connections = true,
                UseCookies = false
            });

            services.RemoveAll<IHttpMessageHandlerBuilderFilter>();
            #endregion

            services.Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            if (AppInit.conf.listen.compression)
            {
                services.AddResponseCompression(options =>
                {
                    options.MimeTypes = AppInit.CompressionMimeTypes;
                });
            }

            services.AddMemoryCache(o =>
            {
                o.TrackStatistics = AppInit.conf.openstat.enable;
            });

            services.AddSignalR(o =>
            {
                o.EnableDetailedErrors = true;
                o.MaximumParallelInvocationsPerClient = 2;
                o.MaximumReceiveMessageSize = 1024 * 1024 * 10; // 10MB
                o.StreamBufferCapacity = 1024 * 1024;           // 1MB
            });

            services.AddSingleton<IActionDescriptorChangeProvider>(DynamicActionDescriptorChangeProvider.Instance);
            services.AddSingleton(DynamicActionDescriptorChangeProvider.Instance);

            services.AddDbContextFactory<SyncUserContext>(SyncUserContext.ConfiguringDbBuilder);
            services.AddDbContextFactory<HybridCacheContext>(HybridCacheContext.ConfiguringDbBuilder);
            services.AddDbContextFactory<ProxyLinkContext>(ProxyLinkContext.ConfiguringDbBuilder);
            services.AddDbContextFactory<SisiContext>(SisiContext.ConfiguringDbBuilder);
            services.AddDbContextFactory<ExternalidsContext>(ExternalidsContext.ConfiguringDbBuilder);

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

            ModuleRepository.Configuration(mvcBuilder);

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

            //  dll  source
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
                            string _file = file.Replace("\\", "/").Replace(path.Replace("\\", "/"), "").Replace(Environment.CurrentDirectory.Replace("\\", "/"), "");
                            if (Regex.IsMatch(_file, "(\\.vs|bin|obj|Properties)/", RegexOptions.IgnoreCase))
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
                                string dlrns = Path.Combine(Environment.CurrentDirectory, "module", "references", refns);
                                if (!File.Exists(dlrns))
                                    dlrns = Path.Combine(Environment.CurrentDirectory, "module", mod.dll, refns);

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
                                WatchersDynamicModule(null, mvcBuilder, mod, path);
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

                if (references != null)
                    CSharpEval.appReferences = references;
            }

            if (AppInit.modules != null)
                AppInit.modules = AppInit.modules.OrderBy(i => i.index).ToList();

            Console.WriteLine();
            #endregion
        }
        #endregion


        public void Configure(
            IApplicationBuilder app, IWebHostEnvironment env, IMemoryCache memory, IHttpClientFactory httpClientFactory, IHostApplicationLifetime applicationLifetime,
            IDbContextFactory<SyncUserContext> SyncUserContextFactory,
            IDbContextFactory<HybridCacheContext> HybridCacheContextFactory,
            IDbContextFactory<ProxyLinkContext> ProxyLinkContextFactory,
            IDbContextFactory<SisiContext> SisiContextFactory,
            IDbContextFactory<ExternalidsContext> ExternalidsContextFactory
        )
        {
            _app = app;
            memoryCache = memory;

            SyncUserContext.Factory = SyncUserContextFactory;
            HybridCacheContext.Factory = HybridCacheContextFactory;
            ProxyLinkContext.Factory = ProxyLinkContextFactory;
            SisiContext.Factory = SisiContextFactory;
            ExternalidsContext.Factory = ExternalidsContextFactory;

            Shared.Startup.Configure(app, memory);
            HybridCache.Configure(memory);
            ProxyManager.Configure(memory);

            Http.httpClientFactory = httpClientFactory;
            nws.memoryCache = memoryCache;
            Http.nws = new nws();
            Http.ws = new soks();

            #region modules loaded
            if (AppInit.modules != null)
            {
                foreach (var mod in AppInit.modules)
                {
                    try
                    {
                        if (mod.dll == "DLNA.dll")
                            mod.initspace = "DLNA.ModInit";

                        if (mod.dll == "SISI.dll")
                            mod.initspace = "SISI.ModInit";

                        if (mod.dll == "Tracks.dll" || mod.dll == "TorrServer.dll")
                            mod.version = 2;

                        LoadedModule(app, mod);
                    }
                    catch (Exception ex) { Console.WriteLine($"Module {mod.NamespacePath(mod.initspace)}: {ex.Message}\n\n"); }
                }
            }
            #endregion

            if (!AppInit.conf.multiaccess || AppInit.conf.useDeveloperExceptionPage)
                app.UseDeveloperExceptionPage();

            applicationLifetime.ApplicationStopping.Register(OnShutdown);

            applicationLifetime.ApplicationStarted.Register(() =>
            {
                if (!string.IsNullOrEmpty(AppInit.conf.listen.sock))
                    _ = Bash.Run($"while [ ! -S /var/run/{AppInit.conf.listen.sock}.sock ]; do sleep 1; done && chmod 666 /var/run/{AppInit.conf.listen.sock}.sock").ConfigureAwait(false);
            });

            #region UseForwardedHeaders
            var forwarded = new ForwardedHeadersOptions
            {
                ForwardLimit = null,
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };

            if (AppInit.conf.KnownProxies != null && AppInit.conf.KnownProxies.Count > 0)
            {
                foreach (var k in AppInit.conf.KnownProxies)
                    forwarded.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse(k.ip), k.prefixLength));
            }

            app.UseForwardedHeaders(forwarded);
            #endregion

            app.UseModHeaders();
            app.UseRequestInfo();

            app.Map("/nws", nwsApp =>
            {
                nwsApp.UseWAF();
                nwsApp.UseWebSockets();
                nwsApp.Run(nws.HandleWebSocketAsync);
            });

            app.Map("/ws", wsApp =>
            {
                wsApp.UseWAF();
                wsApp.UseRouting();
                wsApp.UseEndpoints(endpoints =>
                {
                    endpoints.MapHub<soks>("");
                });
            });

            app.UseStaticache();

            app.UseRouting();

            if (AppInit.conf.listen.compression)
                app.UseResponseCompression();

            app.UseRequestStatistics();
            app.UseAnonymousRequest();

            app.UseAlwaysRjson();
            app.UseModule(first: true);
            app.UseOverrideResponse(first: true);

            #region UseStaticFiles
            app.UseStaticFiles(new StaticFileOptions
            {
                ServeUnknownFileTypes = true,
                DefaultContentType = "application/octet-stream",
                ContentTypeProvider = new FileExtensionContentTypeProvider()
                {
                    Mappings =
                    {
                        [".m4s"]  = "video/mp4",
                        [".ts"]   = "video/mp2t",
                        [".mp4"]  = "video/mp4",
                        [".mkv"]  = "video/x-matroska",
                        [".m3u"]  = "application/x-mpegURL",
                        [".m3u8"] = "application/vnd.apple.mpegurl",
                        [".webm"] = "video/webm",
                        [".mov"]  = "video/quicktime",
                        [".avi"]  = "video/x-msvideo",
                        [".wmv"]  = "video/x-ms-wmv",
                        [".flv"]  = "video/x-flv",
                        [".ogv"]  = "video/ogg",
                        [".m2ts"] = "video/MP2T",
                        [".vob"]  = "video/x-ms-vob",

                        [".apk"]  = "application/vnd.android.package-archive",
                        [".aab"]  = "application/vnd.android.appbundle",
                        [".xapk"]  = "application/vnd.android.package-archive",
                        [".apkm"]  = "application/vnd.android.package-archive",
                        [".obb"]  = "application/octet-stream",

                        [".exe"]  = "application/vnd.microsoft.portable-executable",
                        [".msi"]  = "application/x-msi",
                        [".bat"]  = "application/x-msdownload",
                        [".cmd"]  = "application/x-msdownload",
                        [".msix"]        = "application/msix",
                        [".msixbundle"]  = "application/msixbundle",
                        [".appx"]        = "application/appx",
                        [".appxbundle"]  = "application/appxbundle",

                        [".deb"]  = "application/vnd.debian.binary-package",
                        [".rpm"]  = "application/x-rpm",
                        [".sh"]   = "application/x-sh",
                        [".bin"]  = "application/octet-stream",
                        [".run"]  = "application/x-msdownload",
                        [".appimage"] = "application/octet-stream",

                        [".pkg"]  = "application/octet-stream",
                        [".dmg"]  = "application/x-apple-diskimage",

                        [".zip"] = "application/zip",
                        [".rar"] = "application/vnd.rar",
                        [".7z"]  = "application/x-7z-compressed",
                        [".gz"]  = "application/gzip",
                        [".tar"] = "application/x-tar",
                        [".tgz"] = "application/gzip",

                        [".iso"] = "application/x-iso9660-image"
                    }
                }
            });
            #endregion

            app.UseWAF();
            app.UseAccsdb();

            app.MapWhen(context => context.Request.Path.Value.StartsWith("/proxy/") || context.Request.Path.Value.StartsWith("/proxy-dash/"), proxyApp =>
            {
                proxyApp.UseProxyAPI();
            });

            app.MapWhen(context => context.Request.Path.Value.StartsWith("/proxyimg"), proxyApp =>
            {
                proxyApp.UseProxyIMG();
            });

            app.MapWhen(context => context.Request.Path.Value.StartsWith("/cub/"), proxyApp =>
            {
                proxyApp.UseProxyCub();
            });

            app.MapWhen(context => context.Request.Path.Value.StartsWith("/tmdb/"), proxyApp =>
            {
                proxyApp.UseProxyTmdb();
            });

            app.UseModule(first: false);
            app.UseOverrideResponse(first: false);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }


        #region OnShutdown
        void OnShutdown()
        {
            if (Program._reload)
                return;

            IsShutdown = true;
            Shared.Startup.IsShutdown = true;

            Chromium.FullDispose();
            Firefox.FullDispose();
            nws.FullDispose();
            soks.FullDispose();

            DisposeModule(null);
        }
        #endregion

        #region WatchRebuildModule
        static readonly Dictionary<string, FileSystemWatcher> moduleWatchers = new();

        static readonly object moduleWatcherLock = new object();

        void WatchersDynamicModule(IApplicationBuilder app, IMvcBuilder mvcBuilder, RootModule mod, string path)
        {
            if (!mod.dynamic)
                return;

            path = Path.GetFullPath(path);

            lock (moduleWatcherLock)
            {
                if (moduleWatchers.ContainsKey(path))
                    return;

                var watcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                };

                watcher.Filters.Add("*.cs");
                watcher.Filters.Add("manifest.json");

                CancellationTokenSource debounceCts = null;
                object debounceLock = new object();

                void Recompile(object sender, FileSystemEventArgs e)
                {
                    string _file = e.FullPath.Replace("\\", "/").Replace(path.Replace("\\", "/"), "").Replace(Environment.CurrentDirectory.Replace("\\", "/"), "");
                    if (Regex.IsMatch(_file, "(\\.vs|bin|obj|Properties)/", RegexOptions.IgnoreCase))
                        return;

                    CancellationTokenSource cts;

                    lock (debounceLock)
                    {
                        debounceCts?.Cancel();
                        debounceCts = new CancellationTokenSource();
                        cts = debounceCts;
                    }

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

                        if (cts.IsCancellationRequested)
                            return;

                        watcher.EnableRaisingEvents = false;

                        try
                        {
                            var parts = mvcBuilder.PartManager.ApplicationParts
                                .OfType<AssemblyPart>()
                                .Where(p => p.Assembly == mod.assembly)
                                .ToList();

                            #region update manifest.json
                            string manifestPath = Path.Combine(path, "manifest.json");
                            RootModule manifestMod = null;

                            if (File.Exists(manifestPath))
                            {
                                try
                                {
                                    manifestMod = JsonConvert.DeserializeObject<RootModule>(File.ReadAllText(manifestPath));

                                    var excludedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                    {
                                        nameof(RootModule.dynamic),
                                        nameof(RootModule.index),
                                        nameof(RootModule.dll),
                                        nameof(RootModule.assembly),
                                        nameof(RootModule.initspace)
                                    };

                                    foreach (var property in typeof(RootModule).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                                    {
                                        if (!property.CanRead || !property.CanWrite || excludedProperties.Contains(property.Name))
                                            continue;

                                        property.SetValue(mod, property.GetValue(manifestMod));
                                    }
                                }
                                catch (Exception manifestEx)
                                {
                                    Console.WriteLine($"Failed to update manifest for {mod.dll}: {manifestEx.Message}");
                                }
                            }
                            #endregion

                            var assembly = CSharpEval.Compilation(mod);
                            if (assembly != null)
                            {
                                DisposeModule(mod);

                                foreach (var part in parts)
                                    mvcBuilder.PartManager.ApplicationParts.Remove(part);

                                if (manifestMod != null)
                                    mod.initspace = manifestMod.initspace;

                                mod.assembly = assembly;
                                LoadedModule(app, mod);

                                mvcBuilder.PartManager.ApplicationParts.Add(new AssemblyPart(mod.assembly));
                                DynamicActionDescriptorChangeProvider.Instance.NotifyChanges();

                                MiddlewaresModuleEntry.EnsureCache(forced: true);
                                OnlineModuleEntry.EnsureCache(forced: true);
                                SisiModuleEntry.EnsureCache(forced: true);

                                Console.WriteLine("rebuild module: " + mod.dll);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to rebuild module {mod.dll}: {ex.Message}");
                        }
                        finally
                        {
                            watcher.EnableRaisingEvents = true;
                        }
                    });
                }

                watcher.Changed += Recompile;
                watcher.Created += Recompile;
                watcher.Deleted += Recompile;
                watcher.Renamed += Recompile;

                watcher.EnableRaisingEvents = true;
                moduleWatchers[path] = watcher;
            }
        }
        #endregion

        #region LoadedModule
        void LoadedModule(IApplicationBuilder app, RootModule mod)
        {
            if (mod == null)
                return;

            if (mod.initspace != null && mod.assembly.GetType(mod.NamespacePath(mod.initspace)) is Type t && t.GetMethod("loaded") is MethodInfo m)
            {
                if (mod.version >= 2)
                {
                    m.Invoke(null, [
                        new InitspaceModel()
                        {
                            path = $"module/{mod.dll}",
                            soks = new soks(),
                            nws = new nws(),
                            memoryCache = memoryCache,
                            configuration = Configuration,
                            services = serviceCollection,
                            app = app ?? _app
                        }
                    ]);
                }
                else
                    m.Invoke(null, []);
            }
        }
        #endregion

        #region DisposeModule
        void DisposeModule(RootModule module)
        {
            if (AppInit.modules == null)
                return;

            if (module != null)
            {
                try
                {
                    if (module.initspace != null && module.assembly.GetType(module.NamespacePath(module.initspace)) is Type t && t.GetMethod("Dispose") is MethodInfo m)
                        m.Invoke(null, []);
                }
                catch { }
            }
            else
            {
                foreach (var mod in AppInit.modules)
                {
                    try
                    {
                        if (mod.initspace != null && mod.assembly.GetType(mod.NamespacePath(mod.initspace)) is Type t && t.GetMethod("Dispose") is MethodInfo m)
                            m.Invoke(null, []);
                    }
                    catch { }
                }
            }
        }
        #endregion
    }
}

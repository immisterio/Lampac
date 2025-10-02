using Microsoft.Playwright;
using Shared.Engine;
using Shared.Models.Browser;
using System.Runtime.InteropServices;
using System.Threading;

namespace Shared.PlaywrightCore
{
    public class Firefox : PlaywrightBase, IDisposable
    {
        #region static
        static List<KeepopenPage> pages_keepopen = new();

        public static long stats_keepopen { get; set; }

        public static long stats_newcontext { get; set; }

        static IPlaywright playwright = null;
        static IBrowser browser = null;

        static bool shutdown = false;

        public static PlaywrightStatus Status { get; private set; } = PlaywrightStatus.disabled;

        public static int ContextsCount => browser?.Contexts?.Count ?? 0;

        async public static Task CreateAsync()
        {
            try
            {
                var init = AppInit.conf.firefox;
                if (!init.enable || browser != null || shutdown)
                    return;

                string executablePath = init.executablePath;

                #region Download firefox
                if (string.IsNullOrEmpty(executablePath))
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        switch (RuntimeInformation.ProcessArchitecture)
                        {
                            case Architecture.X86:
                            case Architecture.X64:
                                {
                                    string camoufox = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "x86_64" : "i686";
                                    string uri = $"https://github.com/immisterio/playwright/releases/download/chrome/camoufox-135.0.1-beta.23-win.{camoufox}.zip";
                                    bool res = await DownloadFile(uri, ".playwright/firefox/release.zip", "firefox/");
                                    if (!res)
                                    {
                                        Console.WriteLine("Firefox: error download firefox.zip");
                                        return;
                                    }

                                    executablePath = ".playwright\\firefox\\camoufox.exe";
                                    break;
                                }
                            default:
                                Console.WriteLine("Firefox: Architecture unknown");
                                return;
                        }
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        switch (RuntimeInformation.ProcessArchitecture)
                        {
                            case Architecture.X64:
                            case Architecture.Arm64:
                                {
                                    string camoufox = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "x86_64" : "arm64";
                                    string uri = $"https://github.com/immisterio/playwright/releases/download/chrome/camoufox-135.0.1-beta.23-mac.{camoufox}.zip";
                                    bool res = await DownloadFile(uri, ".playwright/camoufox.zip");
                                    if (!res)
                                    {
                                        Console.WriteLine("Firefox: error download camoufox.zip");
                                        return;
                                    }

                                    Bash.Invoke($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/Camoufox.app/Contents/MacOS/camoufox")}");
                                    executablePath = ".playwright/Camoufox.app/Contents/MacOS/camoufox";
                                    await Task.Delay(TimeSpan.FromSeconds(4));
                                    break;
                                }
                            default:
                                Console.WriteLine("Firefox: Architecture unknown");
                                return;
                        }
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        string camoufox = null;

                        switch (RuntimeInformation.ProcessArchitecture)
                        {
                            case Architecture.X86:
                                camoufox = "i686";
                                break;
                            case Architecture.X64:
                                camoufox = "x86_64";
                                break;
                            case Architecture.Arm64:
                                camoufox = "arm64";
                                break;
                            default:
                                Console.WriteLine("Firefox: Architecture unknown");
                                return;
                        }

                        if (camoufox != null)
                        {
                            string uri = $"https://github.com/immisterio/playwright/releases/download/chrome/camoufox-135.0.1-beta.23-lin.{camoufox}.zip";
                            bool res = await DownloadFile(uri, ".playwright/camoufox.zip", "firefox/");
                            if (!res)
                            {
                                Console.WriteLine("Firefox: error download camoufox.zip");
                                return;
                            }

                            Bash.Invoke($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/firefox/camoufox")}");
                            executablePath = ".playwright/firefox/camoufox";
                            await Task.Delay(TimeSpan.FromSeconds(4));
                        }
                    }
                    else
                    {
                        Console.WriteLine("Firefox: IsOSPlatform unknown");
                        return;
                    }
                }
                #endregion

                if (string.IsNullOrEmpty(executablePath))
                {
                    Console.WriteLine("Firefox: firefox is not installed, please specify full path in executablePath");
                    return;
                }

                Console.WriteLine("Firefox: Initialization");

                playwright = await Playwright.CreateAsync();

                Console.WriteLine("Firefox: CreateAsync");

                browser = await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = init.Headless,
                    ExecutablePath = executablePath,
                    Args = init.Args
                });

                Console.WriteLine("Firefox: LaunchAsync");

                Status = init.Headless ? PlaywrightStatus.headless : PlaywrightStatus.NoHeadless;
                Console.WriteLine($"Firefox: v{browser.Version} / {Status.ToString()} / {browser.IsConnected}");

                browser.Disconnected += Browser_Disconnected;
            }
            catch (Exception ex) 
            {
                Status = PlaywrightStatus.disabled;
                Console.WriteLine($"Firefox: {ex.Message}"); 
            }
        }

        async private static void Browser_Disconnected(object sender, IBrowser e)
        {
            Status = PlaywrightStatus.disabled;
            browser.Disconnected -= Browser_Disconnected;
            Console.WriteLine("Firefox: Browser_Disconnected");

            if (pages_keepopen != null)
                pages_keepopen.Clear();

            try
            {
                await browser.CloseAsync();
                await browser.DisposeAsync();
            }
            catch { }

            browser = null;

            try
            {
                playwright.Dispose();
            }
            catch { }

            playwright = null;
            pages_keepopen = new();
            await Task.Delay(TimeSpan.FromSeconds(10));
            await CreateAsync();
        }
        #endregion

        #region CronStart
        public static void CronStart()
        {
            _closeLifetimeTimer = new Timer(CronCloseLifetimeContext, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        static Timer _closeLifetimeTimer;

        static bool _cronCloseLifetimeWork = false;
        #endregion

        #region CronCloseLifetimeContext
        async static void CronCloseLifetimeContext(object state)
        {
            if (!AppInit.conf.firefox.enable || Status == PlaywrightStatus.disabled)
                return;

            if (_cronCloseLifetimeWork)
                return;

            _cronCloseLifetimeWork = true;

            try
            {
                var init = AppInit.conf.firefox;
                if (0 >= init.context.keepalive)
                    return;

                foreach (var k in pages_keepopen.ToArray())
                {
                    if (Math.Max(1, init.context.min) >= pages_keepopen.Count)
                        break;

                    if (DateTime.Now > k.lastActive.AddMinutes(init.context.keepalive))
                    {
                        try
                        {
                            await k.page.CloseAsync().ConfigureAwait(false);
                            pages_keepopen.Remove(k);
                        }
                        catch { }
                    }
                }
            }
            catch { }
            finally
            {
                _cronCloseLifetimeWork = false;
            }
        }
        #endregion


        public bool IsCompleted { get; set; }

        public string failedUrl { get; set; }

        IPage page { get; set; }

        KeepopenPage keepopen_page { get; set; }


        async public Task<IPage> NewPageAsync(string plugin, Dictionary<string, string> headers = null, (string ip, string username, string password) proxy = default, bool keepopen = true)
        {
            try
            {
                if (browser == null)
                    return null;

                if (proxy != default)
                {
                    #region proxy NewContext
                    if (keepopen)
                    {
                        foreach (var pg in pages_keepopen.ToArray().Where(i => i.proxy != default))
                        {
                            if (pg.plugin == plugin)
                            {
                                if (pg.proxy.ip != proxy.ip || pg.proxy.username != proxy.username || pg.proxy.password != proxy.password)
                                {
                                    _ = pg.page.CloseAsync().ConfigureAwait(false);
                                    pages_keepopen.Remove(pg);
                                    continue;
                                }
                            }

                            if (pg.proxy.ip == proxy.ip && pg.proxy.username == proxy.username && pg.proxy.password == proxy.password)
                            {
                                stats_keepopen++;
                                pg.busy = true;
                                keepopen_page = pg;
                                page = pg.page;
                                page.RequestFailed += Page_RequestFailed;

                                if (headers != null && headers.Count > 0)
                                    await page.SetExtraHTTPHeadersAsync(headers).ConfigureAwait(false);

                                return page;
                            }
                        }
                    }

                    var contextOptions = new BrowserNewContextOptions
                    {
                        Proxy = new Proxy 
                        { 
                            Server = proxy.ip,
                            Bypass = "127.0.0.1",
                            Username = proxy.username,
                            Password = proxy.password
                        }
                    };

                    stats_newcontext++;
                    var context = await browser.NewContextAsync(contextOptions).ConfigureAwait(false);
                    page = await context.NewPageAsync().ConfigureAwait(false);
                    #endregion
                }
                else
                {
                    #region NewContext
                    if (keepopen)
                    {
                        foreach (var pg in pages_keepopen.Where(i => i.proxy == default))
                        {
                            if (pg.busy == false && DateTime.Now > pg.lockTo)
                            {
                                stats_keepopen++;
                                pg.busy = true;
                                keepopen_page = pg;
                                page = pg.page;
                                page.RequestFailed += Page_RequestFailed;

                                if (headers != null && headers.Count > 0)
                                    await page.SetExtraHTTPHeadersAsync(headers).ConfigureAwait(false);

                                return page;
                            }
                        }
                    }

                    stats_newcontext++;
                    page = await browser.NewPageAsync().ConfigureAwait(false);
                    #endregion
                }

                if (headers != null && headers.Count > 0)
                    await page.SetExtraHTTPHeadersAsync(headers).ConfigureAwait(false);

                page.Popup += Page_Popup;
                page.Download += Page_Download;

                if (!keepopen || !AppInit.conf.firefox.context.keepopen || pages_keepopen.Count >= Math.Max(AppInit.conf.firefox.context.min, AppInit.conf.firefox.context.max))
                {
                    page.RequestFailed += Page_RequestFailed;
                    return page;
                }

                keepopen_page = new KeepopenPage() { page = page, busy = true, plugin = plugin, proxy = proxy };
                pages_keepopen.Add(keepopen_page);
                page.RequestFailed += Page_RequestFailed;
                return page;
            }
            catch { return null; }
        }


        void Page_RequestFailed(object sender, IRequest e)
        {
            try
            {
                if (failedUrl != null && e.Url == failedUrl)
                {
                    completionSource.SetResult(null);
                    WebLog(e.Method, e.Url, "RequestFailed", default, e);
                }
            }
            catch { }
        }

        void Page_Download(object sender, IDownload e)
        {
            try
            {
                e.CancelAsync().ConfigureAwait(false);
            }
            catch { }
        }

        void Page_Popup(object sender, IPage e)
        {
            try
            {
                e.CloseAsync().ConfigureAwait(false);
            }
            catch { }
        }


        public void Dispose()
        {
            if (browser == null || AppInit.conf.firefox.DEV)
                return;

            try
            {
                page.RequestFailed -= Page_RequestFailed;

                if (keepopen_page != null)
                {
                    keepopen_page.page.GotoAsync("about:blank").ConfigureAwait(false);
                    keepopen_page.lastActive = DateTime.Now;
                    keepopen_page.lockTo = DateTime.Now.AddSeconds(1);
                    keepopen_page.busy = false;
                }
                else
                {
                    page.Popup -= Page_Popup;
                    page.Download -= Page_Download;
                    page.CloseAsync().ConfigureAwait(false);
                }
            }
            catch { }
        }

        public static void FullDispose()
        {
            shutdown = true;
            if (browser == null)
                return;

            try
            {
                browser.CloseAsync().ContinueWith(t => browser.DisposeAsync());
            }
            catch { }
        }
    }
}

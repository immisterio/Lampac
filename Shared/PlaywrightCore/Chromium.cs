using Microsoft.Playwright;
using Shared.Engine;
using Shared.Models.Browser;
using System.Runtime.InteropServices;
using System.Timers;

namespace Shared.PlaywrightCore
{
    public class Chromium : PlaywrightBase, IDisposable
    {
        #region static
        public static BrowserNewContextOptions baseContextOptions = new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36",
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["accept-language"] = "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5",
                ["sec-ch-ua"] = "\"Not)A;Brand\";v=\"8\", \"Chromium\";v=\"138\", \"Google Chrome\";v=\"138\"",
                ["sec-ch-ua-mobile"] = "?0",
                ["sec-ch-ua-platform"] = "\"Windows\"",
                ["user-agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36"
            }
        };

        static List<KeepopenPage> pages_keepopen = new();

        static IBrowserContext keepopen_context { get; set; }

        static DateTime create_keepopen_context { get; set; }

        public static long stats_keepopen { get; set; }

        public static long stats_newcontext { get; set; }

        public static (DateTime time, int status, string ex) stats_ping { get; set; }


        public static IBrowser browser = null;

        static bool shutdown = false;

        public static PlaywrightStatus Status { get; private set; } = PlaywrightStatus.disabled;

        async public static Task CreateAsync()
        {
            try
            {
                var init = AppInit.conf.chromium;
                if (!init.enable || browser != null || shutdown)
                    return;

                string executablePath = init.executablePath;

                #region Download chromium
                if (string.IsNullOrEmpty(executablePath))
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        switch (RuntimeInformation.ProcessArchitecture)
                        {
                            case Architecture.X86:
                            case Architecture.X64:
                            case Architecture.Arm64:
                                {
                                    string uri = $"https://github.com/immisterio/playwright/releases/download/chrome/chrome-win-{RuntimeInformation.ProcessArchitecture.ToString().ToLower()}.zip";
                                    bool res = await DownloadFile(uri, ".playwright/chrome.zip");
                                    if (!res)
                                    {
                                        Console.WriteLine("Chromium: error download chrome.zip");
                                        return;
                                    }

                                    if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                                        executablePath = ".playwright\\chrome-win32\\chrome.exe";
                                    else
                                        executablePath = ".playwright\\chrome-win\\chrome.exe";
                                    break;
                                }
                            default:
                                Console.WriteLine("Chromium: Architecture unknown");
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
                                    string uri = $"https://github.com/immisterio/playwright/releases/download/chrome/chrome-mac-{RuntimeInformation.ProcessArchitecture.ToString().ToLower()}.zip";
                                    bool res = await DownloadFile(uri, ".playwright/chrome.zip");
                                    if (!res)
                                    {
                                        Console.WriteLine("Chromium: error download chrome.zip");
                                        return;
                                    }

                                    Bash.Invoke($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/chrome-mac/Chromium.app/Contents/MacOS/Chromium")}");
                                    executablePath = ".playwright/chrome-mac/Chromium.app/Contents/MacOS/Chromium";
                                    await Task.Delay(TimeSpan.FromSeconds(4));
                                    break;
                                }
                            default:
                                Console.WriteLine("Chromium: Architecture unknown");
                                return;
                        }
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        switch (RuntimeInformation.ProcessArchitecture)
                        {
                            case Architecture.X86:
                            case Architecture.X64:
                                {
                                    string uri = $"https://github.com/immisterio/playwright/releases/download/chrome/chrome-linux-{RuntimeInformation.ProcessArchitecture.ToString().ToLower()}.zip";
                                    bool res = await DownloadFile(uri, ".playwright/chrome.zip");
                                    if (!res)
                                    {
                                        Console.WriteLine("Chromium: error download chrome.zip");
                                        return;
                                    }

                                    Bash.Invoke($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/chrome-linux/chrome")}");
                                    executablePath = ".playwright/chrome-linux/chrome";
                                    await Task.Delay(TimeSpan.FromSeconds(4));
                                    break;
                                }
                            default:
                                Console.WriteLine("PlaywChromiumright: Architecture unknown");
                                return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Chromium: IsOSPlatform unknown");
                        return;
                    }
                }
                #endregion

                if (string.IsNullOrEmpty(executablePath))
                {
                    Console.WriteLine("Chromium: chromium is not installed, please specify full path in executablePath");
                    return;
                }

                Console.WriteLine("Chromium: Initialization");

                var playwright = await Playwright.CreateAsync();

                Console.WriteLine("Chromium: CreateAsync");

                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = init.Headless,
                    ExecutablePath = executablePath,
                    Args = init.Args,
                    Devtools = init.Devtools
                });

                Console.WriteLine("Chromium: LaunchAsync");

                Status = init.Headless ? PlaywrightStatus.headless : PlaywrightStatus.NoHeadless;
                Console.WriteLine($"Chromium: v{browser.Version} / {Status.ToString()} / {browser.IsConnected}");

                if (AppInit.conf.chromium.context.keepopen)
                {
                    create_keepopen_context = DateTime.Now;
                    var kpc = await browser.NewContextAsync(baseContextOptions);
                    await kpc.NewPageAsync();
                    keepopen_context = kpc;
                }
            }
            catch (Exception ex) 
            {
                Status = PlaywrightStatus.disabled;
                Console.WriteLine($"Chromium: {ex.Message}"); 
            }
        }
        #endregion

        #region CloseLifetimeContext
        async public static Task CloseLifetimeContext()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);

                try
                {
                    var init = AppInit.conf.chromium;
                    if (0 >= init.context.keepalive)
                        continue;

                    if (init.context.keepopen && DateTime.Now > create_keepopen_context.AddMinutes(init.context.keepalive))
                    {
                        create_keepopen_context = DateTime.Now;
                        var kpc = await browser.NewContextAsync(baseContextOptions).ConfigureAwait(false);
                        await kpc.NewPageAsync().ConfigureAwait(false);

                        try
                        {
                            _= keepopen_context.CloseAsync().ConfigureAwait(false);
                        }
                        catch { }

                        keepopen_context = kpc;
                    }

                    foreach (var k in pages_keepopen.ToArray())
                    {
                        if (init.context.min >= pages_keepopen.Count)
                            break;

                        if (DateTime.Now > k.create.AddMinutes(init.context.keepalive))
                        {
                            try
                            {
                                if (pages_keepopen.Remove(k))
                                    _ = Task.Delay(TimeSpan.FromSeconds(20)).ContinueWith(i => k.context.CloseAsync()).ConfigureAwait(false);
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }
        #endregion

        #region Browser_Disconnected
        async public static Task Browser_Disconnected()
        {
            stats_ping = (DateTime.Now, 1, null);
            await Task.Delay(TimeSpan.FromMinutes(2)).ConfigureAwait(false);

            while (!shutdown)
            {
                await Task.Delay(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
                stats_ping = (DateTime.Now, 2, null);

                if ((AppInit.conf.multiaccess || AppInit.conf.chromium.Headless) && Status != PlaywrightStatus.disabled)
                {
                    try
                    {
                        stats_ping = (DateTime.Now, 3, null);
                        if (AppInit.conf.multiaccess == false && keepopen_context == null)
                            continue;

                        stats_ping = (DateTime.Now, 4, null);
                        if (browser == null && keepopen_context == null)
                            continue;

                        bool isOk = false;

                        try
                        {
                            stats_ping = (DateTime.Now, 5, null);
                            IPage p = keepopen_context != null ? await keepopen_context.NewPageAsync().ConfigureAwait(false) : await browser.NewPageAsync().ConfigureAwait(false);
                            if (p != null)
                            {
                                var r = await p.GotoAsync($"http://{AppInit.conf.listen.localhost}:{AppInit.conf.listen.port}/api/chromium/ping").ConfigureAwait(false);
                                if (r != null)
                                {
                                    stats_ping = (DateTime.Now, r.Status, null);
                                    if (r.Status == 200)
                                        isOk = true;
                                }

                                await p.CloseAsync().ConfigureAwait(false);
                            }
                        }
                        catch
                        {
                            stats_ping = (DateTime.Now, 500, null);
                        }

                        if (!isOk)
                        {
                            Status = PlaywrightStatus.disabled;
                            Console.WriteLine("\nChromium: Browser_Disconnected");

                            try
                            {
                                if (browser != null)
                                {
                                    _= browser.CloseAsync().ConfigureAwait(false);
                                    _= browser.DisposeAsync().ConfigureAwait(false);
                                }
                            }
                            catch { }

                            browser = null;
                            pages_keepopen = new();
                            keepopen_context = null;
                            await CreateAsync().ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex) 
                    {
                        stats_ping = (DateTime.Now, -1, ex.Message);
                        Console.WriteLine(ex.Message); 
                    }
                }
            }
        }
        #endregion


        public bool IsCompleted { get; set; }

        bool imitationHuman { get; set; }

        bool deferredDispose { get; set; }

        public string failedUrl { get; set; }

        IPage page { get; set; }

        IBrowserContext context { get; set; }

        KeepopenPage keepopen_page { get; set; }


        async public Task<IPage> NewPageAsync(string plugin, Dictionary<string, string> headers = null, (string ip, string username, string password) proxy = default, bool keepopen = true, bool imitationHuman = false, bool deferredDispose = false)
        {
            try
            {
                if (browser == null)
                    return null;

                this.imitationHuman = imitationHuman;
                this.deferredDispose = deferredDispose;

                if (proxy != default)
                {
                    #region NewPageAsync
                    if (keepopen)
                    {
                        foreach (var pg in pages_keepopen.ToArray())
                        {
                            if (pg.plugin == plugin)
                            {
                                if (pg.proxy.ip != proxy.ip || pg.proxy.username != proxy.username || pg.proxy.password != proxy.password)
                                {
                                    _ = pg.context.CloseAsync();
                                    pages_keepopen.Remove(pg);
                                    continue;
                                }
                            }

                            if (pg.proxy.ip == proxy.ip && pg.proxy.username == proxy.username && pg.proxy.password == proxy.password)
                            {
                                stats_keepopen++;
                                keepopen_page = pg;
                                await ClearCookie(pg.context).ConfigureAwait(false);
                                page = await pg.context.NewPageAsync().ConfigureAwait(false);
                                break;
                            }
                        }
                    }

                    if (page == default)
                    {
                        var contextOptions = new BrowserNewContextOptions
                        {
                            Proxy = new Proxy
                            {
                                Server = proxy.ip,
                                Bypass = "127.0.0.1",
                                Username = proxy.username,
                                Password = proxy.password
                            },
                            UserAgent = baseContextOptions.UserAgent,
                            ExtraHTTPHeaders = baseContextOptions.ExtraHTTPHeaders
                        };

                        stats_newcontext++;
                        context = await browser.NewContextAsync(contextOptions).ConfigureAwait(false);
                        page = await context.NewPageAsync().ConfigureAwait(false);
                    }
                    #endregion

                    if (headers != null && headers.Count > 0)
                        await page.SetExtraHTTPHeadersAsync(headers).ConfigureAwait(false);

                    page.Popup += Page_Popup;
                    page.Download += Page_Download;
                    page.RequestFailed += Page_RequestFailed;

                    if (AppInit.conf.chromium.Devtools)
                        await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false); // что бы devtools успел открыться

                    if (!keepopen || keepopen_page != null || !AppInit.conf.chromium.context.keepopen || pages_keepopen.Count >= AppInit.conf.chromium.context.max)
                        return page;

                    await context.NewPageAsync().ConfigureAwait(false); // что-бы context не закрывался с последней закрытой вкладкой
                    if (pages_keepopen.Count >= AppInit.conf.chromium.context.max)
                        return page;

                    // один из контекстов уже использует этот прокси
                    if (proxy != default && pages_keepopen.FirstOrDefault(i => i.proxy.ip == proxy.ip && i.proxy.username == proxy.username && i.proxy.password == proxy.password)?.proxy != default)
                        return page;

                    keepopen_page = new KeepopenPage() { context = context, plugin = plugin, proxy = proxy };
                    pages_keepopen.Add(keepopen_page);
                    return page;
                }
                else
                {
                    #region NewPageAsync
                    if (keepopen && keepopen_context != default)
                    {
                        stats_keepopen++;
                        await ClearCookie(keepopen_context).ConfigureAwait(false);
                        page = await keepopen_context.NewPageAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        stats_newcontext++;
                        page = await browser.NewPageAsync().ConfigureAwait(false);
                    }
                    #endregion

                    if (headers != null && headers.Count > 0)
                        await page.SetExtraHTTPHeadersAsync(headers).ConfigureAwait(false);

                    page.Popup += Page_Popup;
                    page.Download += Page_Download;
                    page.RequestFailed += Page_RequestFailed;

                    if (AppInit.conf.chromium.Devtools)
                        await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false); // что бы devtools успел открыться

                    return page;
                }
            }
            catch { return null; }
        }


        static bool workClearCookie = false;

        async Task ClearCookie(IBrowserContext context)
        {
            if (workClearCookie)
                return;

            try
            {
                workClearCookie = true;
                var cookies = await context.CookiesAsync();

                foreach (var cookie in cookies.Where(c => c.Name == "cf_clearance"))
                {
                    await context.ClearCookiesAsync(new BrowserContextClearCookiesOptions
                    {
                        Name = cookie.Name,
                        Domain = cookie.Domain,
                        Path = cookie.Path
                    });
                }
            }
            catch { }

            workClearCookie = false;
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
            if (browser == null || AppInit.conf.chromium.DEV)
                return;

            try
            {
                void close()
                {
                    page.RequestFailed -= Page_RequestFailed;
                    page.Popup -= Page_Popup;
                    page.Download -= Page_Download;

                    if (keepopen_page != null)
                    {
                        page.CloseAsync().ConfigureAwait(false);
                        keepopen_page.lastActive = DateTime.Now;
                    }
                    else if (context != null)
                    {
                        context.CloseAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        page.CloseAsync().ConfigureAwait(false);
                    }
                }

                if (imitationHuman)
                {
                    var timer = new Timer(10_000);
                    timer.Elapsed += (s,e) => { close(); };
                    timer.AutoReset = false;
                    timer.Start();
                }
                else if (deferredDispose)
                {
                    var timer = new Timer(2_000);
                    timer.Elapsed += (s, e) => { close(); };
                    timer.AutoReset = false;
                    timer.Start();
                }
                else
                {
                    close();
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
                browser.CloseAsync().Wait(2000);
                browser.DisposeAsync();
            }
            catch { }
        }
    }
}

using Lampac;
using Microsoft.Playwright;
using Shared.Models.Browser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Shared.Engine
{
    public class Chromium : PlaywrightBase, IDisposable
    {
        #region static
        static IBrowser browser = null;

        static bool shutdown = false;

        public static PlaywrightStatus Status { get; private set; } = PlaywrightStatus.disabled;

        async public static ValueTask CreateAsync()
        {
            try
            {
                var init = AppInit.conf.chromium;
                if (!init.enable || browser != null || shutdown)
                    return;

                if (init.DISPLAY != null)
                    Environment.SetEnvironmentVariable("DISPLAY", init.DISPLAY);
                else if (File.Exists("/tmp/.X99-lock"))
                    Environment.SetEnvironmentVariable("DISPLAY", ":99");

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

                                    await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/chrome-mac/Chromium.app/Contents/MacOS/Chromium")}");
                                    executablePath = ".playwright/chrome-mac/Chromium.app/Contents/MacOS/Chromium";
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

                                    await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/chrome-linux/chrome")}");
                                    executablePath = ".playwright/chrome-linux/chrome";
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
                    Args = init.Args
                });

                Console.WriteLine("Chromium: LaunchAsync");

                Status = init.Headless ? PlaywrightStatus.headless : PlaywrightStatus.NoHeadless;
                Console.WriteLine($"Chromium: v{browser.Version} / {Status.ToString()} / {browser.IsConnected}");

                browser.Disconnected += Browser_Disconnected;
            }
            catch (Exception ex) 
            {
                Status = PlaywrightStatus.disabled;
                Console.WriteLine($"Chromium: {ex.Message}"); 
            }
        }

        async private static void Browser_Disconnected(object sender, IBrowser e)
        {
            browser = null;
            pages_keepopen = new();
            Status = PlaywrightStatus.disabled;
            Console.WriteLine("Chromium: Browser_Disconnected");
            await Task.Delay(TimeSpan.FromSeconds(10));
            await CreateAsync();
        }
        #endregion


        string plugin { get; set; }

        public string failedUrl { get; set; }

        IPage page { get; set; }

        KeepopenPage keepopen_page { get; set; }

        static List<KeepopenPage> pages_keepopen = new();


        async public ValueTask<IPage> NewPageAsync(string plugin, Dictionary<string, string> headers = null, (string ip, string username, string password) proxy = default)
        {
            try
            {
                if (browser == null)
                    return null;

                this.plugin = plugin;

                if (proxy != default)
                {
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

                    var context = await browser.NewContextAsync(contextOptions);
                    page = await context.NewPageAsync();
                }
                else
                {
                    foreach (var pg in pages_keepopen)
                    {
                        if (pg.busy == false && DateTime.Now > pg.lockTo)
                        {
                            pg.busy = true;
                            keepopen_page = pg;
                            page = pg.page;
                            page.RequestFailed += Page_RequestFailed;
                            return page;
                        }
                    }

                    page = await browser.NewPageAsync();
                }

                if (headers != null && headers.Count > 0)
                    await page.SetExtraHTTPHeadersAsync(headers);

                page.Popup += Page_Popup;
                page.Download += Page_Download;

                if (proxy != default || AppInit.conf.chromium.keepopen_context == 0 || pages_keepopen.Count >= AppInit.conf.chromium.keepopen_context)
                {
                    page.RequestFailed += Page_RequestFailed;
                    return page;
                }

                keepopen_page = new KeepopenPage() { page = page, busy = true };
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
                e.CancelAsync();
            }
            catch { }
        }

        void Page_Popup(object sender, IPage e)
        {
            try
            {
                e.CloseAsync();
            }
            catch { }
        }


        public void Dispose()
        {
            if (browser == null || AppInit.conf.chromium.DEV)
                return;


            try
            {
                page.RequestFailed -= Page_RequestFailed;

                if (keepopen_page != null)
                {
                    keepopen_page.page.GotoAsync("about:blank");
                    keepopen_page.lastActive = DateTime.Now;
                    keepopen_page.lockTo = DateTime.Now.AddSeconds(1);
                    keepopen_page.busy = false;
                }
                else
                {
                    page.Popup -= Page_Popup;
                    page.Download -= Page_Download;
                    page.CloseAsync();
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


        async public static Task CloseLifetimeContext()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(1));

                try
                {
                    var init = AppInit.conf.chromium;
                    if (0 >= init.keepalive)
                        continue;

                    foreach (var k in pages_keepopen.ToArray())
                    {
                        if (DateTime.Now > k.lastActive.AddMinutes(init.keepalive))
                        {
                            try
                            {
                                await k.page.CloseAsync();
                            }
                            catch { }

                            pages_keepopen.Remove(k);
                        }
                    }
                }
                catch { }
            }
        }
    }
}

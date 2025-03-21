using Lampac;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Shared.Engine
{
    public class Firefox : PlaywrightBase, IDisposable
    {
        #region static
        static IBrowser browser = null;

        static bool shutdown = false;

        public static PlaywrightStatus Status { get; private set; } = PlaywrightStatus.disabled;

        async public static ValueTask CreateAsync()
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

                                    await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/Camoufox.app/Contents/MacOS/camoufox")}");
                                    executablePath = ".playwright/Camoufox.app/Contents/MacOS/camoufox";
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

                            await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/firefox/camoufox")}");
                            executablePath = ".playwright/firefox/camoufox";
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

                var playwright = await Playwright.CreateAsync();

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
            browser = null;
            Status = PlaywrightStatus.disabled;
            Console.WriteLine("Firefox: Browser_Disconnected");
            await Task.Delay(TimeSpan.FromSeconds(10));
            await CreateAsync();
        }
        #endregion


        string plugin { get; set; }

        IPage page { get; set; }

        static Dictionary<string, IPage> pages_keepopen = new();


        async public ValueTask<IPage> NewPageAsync(string plugin, Dictionary<string, string> headers = null, (string ip, string username, string password) proxy = default)
        {
            try
            {
                if (browser == null)
                    return null;

                this.plugin = plugin;

                if (AppInit.conf.firefox.keepopen && pages_keepopen.TryGetValue(plugin, out IPage _pg))
                    return _pg;

                IPage newpage;

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
                    newpage = await context.NewPageAsync();
                }
                else
                {
                    newpage = await browser.NewPageAsync();
                }

                if (headers != null && headers.Count > 0)
                    await newpage.SetExtraHTTPHeadersAsync(headers);

                newpage.Popup += async (sender, e) =>
                {
                    await e.CloseAsync();
                };

                if (AppInit.conf.firefox.keepopen)
                {
                    pages_keepopen.TryAdd(plugin, newpage);
                    return newpage;
                }
                else
                {
                    page = newpage;
                    return page;
                }
            }
            catch { return null; }
        }


        public void Dispose()
        {
            if (browser == null || AppInit.conf.firefox.DEV)
                return;

            try
            {
                if (page != null)
                    page.CloseAsync();

                if (pages_keepopen.TryGetValue(plugin, out IPage _pg))
                    _pg.GotoAsync("about:blank");
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

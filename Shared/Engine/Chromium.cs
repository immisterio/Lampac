using Lampac;
using Lampac.Engine.CORE;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web;

namespace Shared.Engine
{
    public enum ChromiumStatus
    {
        disabled,
        headless,
        NoHeadless
    }

    public class Chromium : IDisposable
    {
        #region static
        static IBrowser browser = null;

        static bool shutdown = false;

        public static ChromiumStatus Status { get; private set; } = ChromiumStatus.disabled;

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

                if (!File.Exists(".playwright/package/index.js"))
                {
                    bool res = await DownloadFile("https://github.com/immisterio/playwright/releases/download/chrome/package.zip", ".playwright/package.zip");
                    if (!res)
                    {
                        Console.WriteLine("Playwright: error download package.zip");
                        return;
                    }
                }

                #region Download node
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    switch (RuntimeInformation.ProcessArchitecture)
                    {
                        case Architecture.X86:
                        case Architecture.X64:
                        case Architecture.Arm64:
                            {
                                string arc = RuntimeInformation.ProcessArchitecture.ToString().ToLower();
                                bool res = await DownloadFile($"https://github.com/immisterio/playwright/releases/download/chrome/node-win-{arc}.exe", $".playwright\\node\\win32_{arc}\\node.exe");
                                if (!res)
                                {
                                    Console.WriteLine($"Playwright: error download node-win-{arc}.exe");
                                    return;
                                }
                                break;
                            }
                        default:
                            Console.WriteLine("Playwright: Architecture unknown");
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
                                string arc = RuntimeInformation.ProcessArchitecture.ToString().ToLower();
                                bool res = await DownloadFile($"https://github.com/immisterio/playwright/releases/download/chrome/node-mac-{arc}", $".playwright/node/mac-{arc}/node");
                                if (!res)
                                {
                                    Console.WriteLine($"Playwright: error download node-mac-{arc}");
                                    return;
                                }

                                await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), $".playwright/node/mac-{arc}/node")}");
                                break;
                            }
                        default:
                            Console.WriteLine("Playwright: Architecture unknown");
                            return;
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    switch (RuntimeInformation.ProcessArchitecture)
                    {
                        case Architecture.X86:
                        case Architecture.X64:
                        case Architecture.Arm64:
                            {
                                string arc = RuntimeInformation.ProcessArchitecture.ToString().ToLower();
                                bool res = await DownloadFile($"https://github.com/immisterio/playwright/releases/download/chrome/node-linux-{arc}", $".playwright/node/linux-{arc}/node");
                                if (!res)
                                {
                                    Console.WriteLine($"Playwright: error download node-linux-{arc}");
                                    return;
                                }

                                await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), $".playwright/node/linux-{arc}/node")}");
                                break;
                            }
                        case Architecture.Arm:
                            {
                                bool res = await DownloadFile("https://github.com/immisterio/playwright/releases/download/chrome/node-linux-armv7l", ".playwright/node/linux-arm/node");
                                if (!res)
                                {
                                    Console.WriteLine("Playwright: error download node-linux-armv7l");
                                    return;
                                }

                                await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/node/linux-arm/node")}");
                                break;
                            }
                        default:
                            Console.WriteLine("Playwright: Architecture unknown");
                            return;
                    }
                }
                else
                {
                    Console.WriteLine("Playwright: IsOSPlatform unknown");
                    return;
                }
                #endregion

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
                                        Console.WriteLine("Playwright: error download chrome.zip");
                                        return;
                                    }

                                    if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                                        executablePath = ".playwright\\chrome-win32\\chrome.exe";
                                    else
                                        executablePath = ".playwright\\chrome-win\\chrome.exe";
                                    break;
                                }
                            default:
                                Console.WriteLine("Playwright: Architecture unknown");
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
                                        Console.WriteLine("Playwright: error download chrome.zip");
                                        return;
                                    }

                                    await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/chrome-mac/Chromium.app/Contents/MacOS/Chromium")}");
                                    executablePath = ".playwright/chrome-mac/Chromium.app/Contents/MacOS/Chromium";
                                    break;
                                }
                            default:
                                Console.WriteLine("Playwright: Architecture unknown");
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
                                        Console.WriteLine("Playwright: error download chrome.zip");
                                        return;
                                    }

                                    await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/chrome-linux/chrome")}");
                                    executablePath = ".playwright/chrome-linux/chrome";
                                    break;
                                }
                            default:
                                Console.WriteLine("Playwright: Architecture unknown");
                                return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Playwright: IsOSPlatform unknown");
                        return;
                    }
                }
                #endregion

                if (string.IsNullOrEmpty(executablePath))
                {
                    Console.WriteLine("Playwright: chromium is not installed, please specify full path in executablePath");
                    return;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && init.Xvfb)
                {
                    _ = Bash.Run("Xvfb :99 -screen 0 1280x1024x24 &").ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    Console.WriteLine("Playwright: Xvfb run");
                }

                Console.WriteLine("Playwright: Initialization ok");

                var playwright = await Playwright.CreateAsync();

                Console.WriteLine("Playwright: CreateAsync");

                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = init.Headless,
                    ExecutablePath = executablePath,
                    Args = init.Args
                });

                Console.WriteLine("Playwright: LaunchAsync");

                Status = init.Headless ? ChromiumStatus.headless : ChromiumStatus.NoHeadless;
                Console.WriteLine($"Playwright: {Status.ToString()}");

                if (browser.IsConnected)
                    Console.WriteLine($"Playwright: {browser.BrowserType.Name} {browser.Version}");

                browser.Disconnected += Browser_Disconnected;
            }
            catch (Exception ex) { Console.WriteLine($"Playwright: {ex.Message}"); }
        }

        async private static void Browser_Disconnected(object sender, IBrowser e)
        {
            browser = null;
            Status = ChromiumStatus.disabled;
            Console.WriteLine("Playwright: Browser_Disconnected");
            await Task.Delay(TimeSpan.FromSeconds(10));
            await CreateAsync();
        }

        public static string IframeUrl(string link) => $"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}/api/chromium/iframe?src={HttpUtility.UrlEncode(link)}";

        async static ValueTask<bool> DownloadFile(string uri, string outfile)
        {
            if (File.Exists($"{outfile}.ok"))
                return true;

            if (File.Exists(outfile))
                File.Delete(outfile);

            Directory.CreateDirectory(Path.GetDirectoryName(outfile));

            if (await HttpClient.DownloadFile(uri, outfile).ConfigureAwait(false))
            {
                File.Create($"{outfile}.ok");

                if (outfile.EndsWith(".zip"))
                {
                    ZipFile.ExtractToDirectory(outfile, ".playwright/", overwriteFiles: true);
                    File.Delete(outfile);
                }

                return true;
            }
            else
            {
                File.Delete(outfile);
                return false;
            }
        }

        public static void WebLog(IRequest request, IResponse response, string result)
        {
            if (request.Url.Contains("127.0.0.1"))
                return;

            string log = $"{DateTime.Now}\n{request.Method}: {request.Url}\n";
            foreach (var item in request.Headers)
                log += $"{item.Key}: {item.Value}\n";

            if (response == null)
            {
                log += "\nresponse null";
                HttpClient.onlog?.Invoke(null, log);
                return;
            }

            log += "\n\n";
            foreach (var item in response.Headers)
                log += $"{item.Key}: {item.Value}\n";

            log += $"\n\n{result}";

            HttpClient.onlog?.Invoke(null, log);
        }
        #endregion


        IPage page { get; set; }

        public TaskCompletionSource<string> completionSource { get; private set; }

        async public ValueTask<IPage> NewPageAsync(Dictionary<string, string> headers = null, (string ip, string username, string password) proxy = default)
        {
            try
            {
                if (browser == null)
                    return null;

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
                    page = await browser.NewPageAsync();
                }

                if (headers != null && headers.Count > 0)
                    await page.SetExtraHTTPHeadersAsync(headers);

                completionSource = new TaskCompletionSource<string>();

                return page;
            }
            catch { return null; }
        }

        async public ValueTask<string> WaitPageResult(int seconds = 8)
        {
            try
            {
                var completionTask = completionSource.Task;
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(seconds));

                var completedTask = await Task.WhenAny(completionTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == completionTask)
                    return await completionTask;

                return null;
            }
            catch { return null; }
        }


        public void Dispose()
        {
            if (browser == null || AppInit.conf.chromium.DEV)
                return;

            try
            {
                if (page != null)
                    page.CloseAsync();
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

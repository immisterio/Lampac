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
                    bool res = await DownloadFile("https://github.com/immisterio/playwright/releases/download/chrome/package.zip", ".playwright\\package.zip");
                    if (!res)
                        return;
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
                                bool res = await DownloadFile($"https://github.com/immisterio/playwright/releases/download/chrome/node-win-{arc}.exe", $".playwright\\node\\win32-{arc}\\node.exe");
                                if (!res)
                                    return;
                                break;
                            }
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
                                bool res = await DownloadFile($"https://github.com/immisterio/playwright/releases/download/chrome/node-mac-{arc}", $".playwright/node/mac_{arc}/node");
                                if (!res)
                                    return;

                                await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), $".playwright/node/mac_{arc}/node")}");
                                break;
                            }
                        default:
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
                                bool res = await DownloadFile($"https://github.com/immisterio/playwright/releases/download/chrome/node-linux-{arc}", $".playwright/node/linux_{arc}/node");
                                if (!res)
                                    return;

                                await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), $".playwright/node/linux_{arc}/node")}");
                                break;
                            }
                        case Architecture.Arm:
                            await DownloadFile("https://github.com/immisterio/playwright/releases/download/chrome/node-linux-armv7l", ".playwright/node/linux_arm/node");
                            await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/node/linux_arm/node")}");
                            break;
                        default:
                            return;
                    }
                }
                else
                {
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
                                        return;

                                    if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                                        executablePath = ".playwright\\chrome-win32\\chrome.exe";
                                    else
                                        executablePath = ".playwright\\chrome-win\\chrome.exe";
                                    break;
                                }
                            default:
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
                                        return;

                                    await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/chrome-mac/Chromium.app/Contents/MacOS/Chromium")}");
                                    executablePath = ".playwright/chrome-mac/Chromium.app/Contents/MacOS/Chromium";
                                    break;
                                }
                            default:
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
                                        return;

                                    await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/chrome-linux/chrome")}");
                                    executablePath = ".playwright/chrome-linux/chrome";
                                    break;
                                }
                            default:
                                return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                #endregion

                if (string.IsNullOrEmpty(executablePath))
                    return;

                var playwright = await Playwright.CreateAsync();

                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = init.Headless,
                    ExecutablePath = executablePath
                });

                Status = init.Headless ? ChromiumStatus.headless : ChromiumStatus.NoHeadless;
                browser.Disconnected += Browser_Disconnected;
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        async private static void Browser_Disconnected(object sender, IBrowser e)
        {
            browser = null;
            Status = ChromiumStatus.disabled;
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

            if (await HttpClient.DownloadFile(uri, outfile))
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
        #endregion


        IPage page { get; set; }

        public TaskCompletionSource<string> completionSource { get; private set; }

        async public ValueTask<IPage> NewPageAsync(Dictionary<string, string> headers = null)
        {
            try
            {
                if (browser == null)
                    return null;

                page = await browser.NewPageAsync();

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

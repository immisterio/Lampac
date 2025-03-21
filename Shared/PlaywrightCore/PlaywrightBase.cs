using Lampac;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Shared.Engine
{
    public enum PlaywrightStatus
    {
        disabled,
        headless,
        NoHeadless
    }

    public class PlaywrightBase
    {
        async public static ValueTask<bool> InitializationAsync()
        {
            try
            {
                if (!AppInit.conf.chromium.enable && !AppInit.conf.firefox.enable)
                    return false;

                if (!File.Exists(".playwright/package/index.js"))
                {
                    bool res = await DownloadFile("https://github.com/immisterio/playwright/releases/download/chrome/package.zip", ".playwright/package.zip");
                    if (!res)
                    {
                        Console.WriteLine("Playwright: error download package.zip");
                        return false;
                    }
                }

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
                                    return false;
                                }
                                break;
                            }
                        default:
                            Console.WriteLine("Playwright: Architecture unknown");
                            return false;
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
                                    return false;
                                }

                                await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), $".playwright/node/mac-{arc}/node")}");
                                break;
                            }
                        default:
                            Console.WriteLine("Playwright: Architecture unknown");
                            return false;
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
                                    return false;
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
                                    return false;
                                }

                                await Bash.Run($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/node/linux-arm/node")}");
                                break;
                            }
                        default:
                            Console.WriteLine("Playwright: Architecture unknown");
                            return false;
                    }
                }
                else
                {
                    Console.WriteLine("Playwright: IsOSPlatform unknown");
                    return false;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && (AppInit.conf.chromium.Xvfb || AppInit.conf.firefox.Xvfb))
                {
                    if (!File.Exists("/usr/bin/Xvfb"))
                        Console.WriteLine("Playwright: /usr/bin/Xvfb not found");

                    _ = Bash.Run("Xvfb :99 -screen 0 1280x1024x24").ConfigureAwait(false);
                    Environment.SetEnvironmentVariable("DISPLAY", ":99");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    Console.WriteLine("Playwright: Xvfb 99");
                }

                Console.WriteLine("Playwright: Initialization");
                return true;
            }
            catch (Exception ex) 
            {
                Console.WriteLine($"Playwright: {ex.Message}");
                return false;
            }
        }
        

        async public static ValueTask<bool> DownloadFile(string uri, string outfile, string folder = null)
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
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        await Bash.Run($"unzip {Path.Combine(Environment.CurrentDirectory, outfile)} -d {Path.Combine(Environment.CurrentDirectory, ".playwright", folder ?? string.Empty)}");
                    }
                    else
                    {
                        ZipFile.ExtractToDirectory(outfile, ".playwright/" + folder, overwriteFiles: true);
                    }

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
        

        public static void WebLog(IRequest request, IResponse response, string result, (string ip, string username, string password) proxy = default)
        {
            if (request.Url.Contains("127.0.0.1"))
                return;

            string log = $"{DateTime.Now}\n";
            if (proxy != default)
                log += $"proxy: {proxy}\n";

            log += $"{request.Method}: {request.Url}\n";

            foreach (var item in request.Headers)
                log += $"{item.Key}: {item.Value}\n";

            if (response == null)
            {
                log += "\nresponse null";
                HttpClient.onlog?.Invoke(null, log);
                return;
            }

            log += $"\n\nCurrentUrl: {response.Url}\nStatusCode: {response.Status}\n";
            foreach (var item in response.Headers)
                log += $"{item.Key}: {item.Value}\n";

            HttpClient.onlog?.Invoke(null, $"{log}\n{result}");
        }

        public static void WebLog(string method, string url, string result, (string ip, string username, string password) proxy = default, IRequest request = default)
        {
            if (url.Contains("127.0.0.1"))
                return;

            string log = $"{DateTime.Now}\n";
            if (proxy != default)
                log += $"proxy: {proxy}\n";

            log += $"{method}: {url}\n";

            if (request?.Headers != null)
            {
                foreach (var item in request.Headers)
                    log += $"{item.Key}: {item.Value}\n";
            }

            HttpClient.onlog?.Invoke(null, $"{log}\n{result}");
        }


        public static string IframeUrl(string link) => $"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}/api/chromium/iframe?src={HttpUtility.UrlEncode(link)}";


        public TaskCompletionSource<string> completionSource { get; private set; } = new TaskCompletionSource<string>();

        async public ValueTask<string> WaitPageResult(int seconds = 10)
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


        async public static Task CacheOrContinue(IMemoryCache memoryCache, IPage page, IRoute route)
        {
            if (Regex.IsMatch(route.Request.Url, "(image.tmdb.org|yandex\\.|google-analytics|yahoo\\.|googletagmanager)"))
            {
                await route.AbortAsync();
                return;
            }

            if (Regex.IsMatch(route.Request.Url, "\\.(woff2?|vtt|css|js|svg|jpe?g|png)$") || Regex.IsMatch(route.Request.Url, "(gstatic|googleapis)\\."))
            {
                if (Regex.IsMatch(route.Request.Url, "/(cdn-cgi|cgi)/"))
                {
                    await route.ContinueAsync();
                    return;
                }

                if (memoryCache.TryGetValue(route.Request.Url, out (byte[] content, Dictionary<string, string> headers) cache))
                {
                    await route.FulfillAsync(new RouteFulfillOptions
                    {
                        BodyBytes = cache.content,
                        Headers = cache.headers
                    });
                }
                else
                {
                    await route.ContinueAsync();
                    var response = await page.WaitForResponseAsync(route.Request.Url);
                    if (response != null)
                    {
                        var content = await response.BodyAsync();
                        if (content != null)
                            memoryCache.Set(route.Request.Url, (content, response.Headers), DateTime.Now.AddDays(1));
                    }
                }

                return;
            }

            await route.ContinueAsync();
        }
    }
}

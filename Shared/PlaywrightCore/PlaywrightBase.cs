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
            try
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
            catch { }
        }

        public static void WebLog(string method, string url, string result, (string ip, string username, string password) proxy = default, IRequest request = default)
        {
            try
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
            catch { }
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


        async public static ValueTask<bool> AbortOrCache(IMemoryCache memoryCache, IPage page, IRoute route, bool abortMedia = false, bool fullCacheJS = false, string patterCache = null)
        {
            try
            {
                if (Regex.IsMatch(route.Request.Url, "(image.tmdb.org|yandex\\.|google-analytics|yahoo\\.|fonts.googleapis|googletagmanager|opensubtitles\\.)"))
                {
                    await route.AbortAsync();
                    return true;
                }

                if (abortMedia && Regex.IsMatch(route.Request.Url, "\\.(woff2?|vtt|srt|css|svg|jpe?g|png|gif|webp|ico)"))
                {
                    Console.WriteLine($"Playwright: Abort {route.Request.Url}");
                    await route.AbortAsync();
                    return true;
                }

                if (route.Request.Method == "GET")
                {
                    bool valid = false;
                    string memkey = route.Request.Url;

                    if (Regex.IsMatch(route.Request.Url, "\\.(woff2?|css|svg|jpe?g|png|gif)") || (fullCacheJS && route.Request.Url.Contains(".js")) || route.Request.Url.Contains(".googleapis.com/css"))
                    {
                        valid = true;
                        memkey = route.Request.Url.Split("?")[0];
                    }
                    else if (Regex.IsMatch(route.Request.Url, "\\.(js|wasm)$"))
                    {
                        valid = true;
                        memkey = route.Request.Url;
                    }
                    else if (patterCache != null && Regex.IsMatch(route.Request.Url, patterCache))
                    {
                        valid = true;
                        memkey = route.Request.Url;
                    }

                    if (valid)
                    {
                        if (memoryCache.TryGetValue(memkey, out (byte[] content, Dictionary<string, string> headers) cache))
                        {
                            Console.WriteLine($"Playwright: CACHE {route.Request.Url}");
                            await route.FulfillAsync(new RouteFulfillOptions
                            {
                                BodyBytes = cache.content,
                                Headers = cache.headers
                            });
                        }
                        else
                        {
                            Console.WriteLine($"Playwright: MISS {route.Request.Url}");
                            await route.ContinueAsync();
                            var response = await page.WaitForResponseAsync(route.Request.Url);
                            if (response != null)
                            {
                                var content = await response.BodyAsync();
                                if (content != null)
                                    memoryCache.Set(memkey, (content, response.Headers), DateTime.Now.AddHours(1));
                            }
                        }

                        return true;
                    }
                }

                Console.WriteLine($"Playwright: {route.Request.Method} {route.Request.Url}");
                return false;
            }
            catch { return false; }
        }
    }
}

using Lampac;
using Lampac.Engine.CORE;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Shared.Engine.CORE;
using Shared.Model.Online;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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
        #region InitializationAsync
        async public static Task<bool> InitializationAsync()
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

                                Bash.Invoke($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), $".playwright/node/mac-{arc}/node")}");
                                await Task.Delay(TimeSpan.FromSeconds(4));
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

                                Bash.Invoke($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), $".playwright/node/linux-{arc}/node")}");
                                await Task.Delay(TimeSpan.FromSeconds(4));
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

                                Bash.Invoke($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), ".playwright/node/linux-arm/node")}");
                                await Task.Delay(TimeSpan.FromSeconds(4));
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

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && (AppInit.conf.chromium.Headless == false || AppInit.conf.firefox.Headless == false))
                {
                    if (!File.Exists("/usr/bin/Xvfb"))
                    {
                        Console.WriteLine("Playwright: install xvfb");
                        await Bash.Run("apt update && apt install -y xvfb libnss3-dev libgdk-pixbuf2.0-dev libgtk-3-dev libxss-dev libasound2");
                    }

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
        #endregion

        #region DownloadFile
        async public static Task<bool> DownloadFile(string uri, string outfile, string folder = null)
        {
            if (File.Exists($"{outfile}.ok"))
                return true;

            if (File.Exists(outfile))
                File.Delete(outfile);

            Directory.CreateDirectory(Path.GetDirectoryName(outfile));

            Console.WriteLine($"Playwright: Download {outfile}");

            if (await HttpClient.DownloadFile(uri, outfile))
            {
                File.Create($"{outfile}.ok");

                if (outfile.EndsWith(".zip"))
                {
                    Console.WriteLine($"Playwright: unzip {outfile}");

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
        #endregion

        #region WebLog
        public static void WebLog(IRequest request, IResponse response, in string result, (string ip, string username, string password) proxy = default)
        {
            try
            {
                if (request.Url.Contains("127.0.0.1") || !AppInit.conf.weblog.enable)
                    return;

                var log = new StringBuilder();

                log.Append($"{DateTime.Now}\n");

                if (proxy != default)
                    log.Append($"proxy: {proxy}\n");

                log.Append($"{request.Method}: {request.Url}\n");

                foreach (var item in request.Headers)
                    log.Append($"{item.Key}: {item.Value}\n");

                if (response == null)
                {
                    log.Append("\nresponse null");
                    HttpClient.onlog?.Invoke(null, log.ToString());
                    return;
                }

                log.Append($"\n\nCurrentUrl: {response.Url}\nStatusCode: {response.Status}\n");
                foreach (var item in response.Headers)
                    log.Append($"{item.Key}: {item.Value}\n");

                HttpClient.onlog?.Invoke(null, $"{log.ToString()}\n{result}");
            }
            catch { }
        }

        public static void WebLog(string method, string url, in string result, (string ip, string username, string password) proxy = default, IRequest request = default, IResponse response = default)
        {
            try
            {
                if (url.Contains("127.0.0.1") || !AppInit.conf.weblog.enable)
                    return;

                var log = new StringBuilder();

                log.Append($"{DateTime.Now}\n");

                if (proxy != default)
                    log.Append($"proxy: {proxy}\n");

                log.Append($"{method}: {url}\n");

                if (request?.Headers != null)
                {
                    foreach (var item in request.Headers)
                        log.Append($"{item.Key}: {item.Value}\n");
                }

                if (response?.Headers != null)
                {
                    log.Append($"\n\nCurrentUrl: {response.Url}\nStatusCode: {response.Status}\n");
                    foreach (var item in response.Headers)
                        log.Append($"{item.Key}: {item.Value}\n");
                }

                HttpClient.onlog?.Invoke(null, $"{log.ToString()}\n{result}");
            }
            catch { }
        }
        #endregion

        #region IframeUrl
        public static string IframeUrl(string link) => $"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}/api/chromium/iframe?src={HttpUtility.UrlEncode(link)}";

        public static string IframeHtml(string link) => $@"<html lang=""ru"">
                <head>
                    <meta charset=""UTF-8"">
                    <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
                </head>
                <body>
                    <iframe width=""560"" height=""400"" src=""{link}"" frameborder=""0"" allow=""*"" allowfullscreen></iframe>
                </body>
            </html>";
        #endregion

        public TaskCompletionSource<string> completionSource { get; private set; } = new TaskCompletionSource<string>();

        #region WaitPageResult
        async public Task<string> WaitPageResult(int seconds = 10)
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
        #endregion

        #region AbortOrCache
        async public static ValueTask<bool> AbortOrCache(IPage page, IRoute route, bool abortMedia = false, bool fullCacheJS = false, string patterCache = null)
        {
            try
            {
                if (Regex.IsMatch(route.Request.Url, "(image.tmdb.org|yandex\\.|google-analytics|yahoo\\.|fonts.googleapis|googletagmanager|opensubtitles\\.|/favicon\\.ico$)"))
                {
                    await route.AbortAsync();
                    return true;
                }

                if (abortMedia && Regex.IsMatch(route.Request.Url.Split("?")[0], "\\.(woff2?|vtt|srt|css|svg|jpe?g|png|gif|webp|ico|ts|m4s)$"))
                {
                    if (AppInit.conf.chromium.consoleLog || AppInit.conf.firefox.consoleLog)
                        Console.WriteLine($"Playwright: Abort {route.Request.Url}");

                    await route.AbortAsync();
                    return true;
                }

                if (route.Request.Method == "GET")
                {
                    bool valid = false;
                    string memkey = route.Request.Url;

                    if ((fullCacheJS && route.Request.Url.Contains(".js")) || Regex.IsMatch(route.Request.Url, "(\\.googleapis\\.com/css|gstatic\\.com|plrjs\\.com)", RegexOptions.IgnoreCase))
                    {
                        valid = true;
                        memkey = route.Request.Url.Split("?")[0];
                    }
                    else if (Regex.IsMatch(route.Request.Url, "\\.(js|wasm)$") || Regex.IsMatch(route.Request.Url, "\\.(css|woff2?|svg|jpe?g|png|gif)"))
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
                        var hybridCache = new HybridCache();
                        if (hybridCache.TryGetValue(memkey, out (byte[] content, Dictionary<string, string> headers) cache))
                        {
                            if (AppInit.conf.chromium.consoleLog || AppInit.conf.firefox.consoleLog)
                                Console.WriteLine($"Playwright: CACHE {route.Request.Url}");

                            await route.FulfillAsync(new RouteFulfillOptions
                            {
                                BodyBytes = cache.content,
                                Headers = cache.headers
                            });
                        }
                        else
                        {
                            if (AppInit.conf.chromium.consoleLog || AppInit.conf.firefox.consoleLog)
                                Console.WriteLine($"Playwright: MISS {route.Request.Url}");

                            await route.ContinueAsync();
                            var response = await page.WaitForResponseAsync(route.Request.Url);
                            if (response != null)
                            {
                                var content = await response.BodyAsync();
                                if (content != null)
                                    hybridCache.Set(memkey, (content, response.Headers), DateTime.Now.AddMinutes(10));
                            }
                        }

                        return true;
                    }
                }

                if (AppInit.conf.chromium.consoleLog || AppInit.conf.firefox.consoleLog)
                    Console.WriteLine($"Playwright: {route.Request.Method} {route.Request.Url}");

                return false;
            }
            catch { return false; }
        }
        #endregion


        public static void GotoAsync(IPage page, string uri)
        {
            _ = page.GotoAsync(uri, new PageGotoOptions() { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
        }


        public static void ConsoleLog(in string value, List<HeadersModel> headers = null)
        {
            if (AppInit.conf.chromium.consoleLog || AppInit.conf.firefox.consoleLog)
            {
                if (headers != null)
                {
                    Console.WriteLine($"\n{value}\n{JsonConvert.SerializeObject(headers.ToDictionary(), Formatting.Indented)}\n");
                }
                else
                {
                    Console.WriteLine(value);
                }
            }
        }
    }
}

using Microsoft.Playwright;
using Shared;
using Shared.Models.Online.Settings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FlixCDN;

static class FlixCdnBrowserResolver
{
    static readonly Serilog.ILogger Log = Serilog.Log.ForContext(typeof(FlixCdnBrowserResolver));
    static readonly SemaphoreSlim resolveSlots = new(2, 2);
    static readonly SemaphoreSlim lifecycleGate = new(1, 1);
    static readonly object idleSync = new();
    static readonly TimeSpan idleLifetime = TimeSpan.FromMinutes(5);

    static IPlaywright playwright;
    static IBrowser browser;
    static Process xvfb;
    static Timer idleTimer;
    static int activeRequests;

    public static async Task<string> ResolveAsync(
        OnlinesSettings init,
        (string ip, string username, string password) proxy,
        string playerUrl,
        int id,
        int translation,
        short season,
        short episode,
        CancellationToken cancellationToken)
    {
        if (!await resolveSlots.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken))
            return null;

        Interlocked.Increment(ref activeRequests);
        CancelIdleShutdown();

        try
        {
            var activeBrowser = await GetBrowserAsync(cancellationToken);
            if (activeBrowser == null)
                return null;

            var options = new BrowserNewContextOptions { ExtraHTTPHeaders = init.headers };
            if (proxy != default)
            {
                options.Proxy = new Proxy
                {
                    Server = proxy.ip,
                    Username = proxy.username,
                    Password = proxy.password
                };
            }

            await using var context = await activeBrowser.NewContextAsync(options);
            var page = await context.NewPageAsync();
            string url = playerUrl + $"&translation={translation}";
            if (season > 0) url += $"&season={season}";
            if (episode > 0) url += $"&episode={episode}";

            string authority = new Uri(playerUrl).Authority;
            var response = await page.RunAndWaitForResponseAsync(
                () => page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 20000
                }),
                response => IsPlaybackResponse(response, authority, id, translation, season, episode),
                new PageRunAndWaitForResponseOptions { Timeout = 20000 }
            ).WaitAsync(cancellationToken);

            if (!response.Ok)
                return null;

            return JsonSerializer.Deserialize<PlayerFiles>(
                await response.TextAsync().WaitAsync(cancellationToken)
            )?.file;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warning("FlixCDN browser access verification failed ({ErrorType})", ex.GetType().Name);
            return null;
        }
        finally
        {
            if (Interlocked.Decrement(ref activeRequests) == 0)
                ScheduleIdleShutdown();

            resolveSlots.Release();
        }
    }


    static async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        await lifecycleGate.WaitAsync(cancellationToken);

        try
        {
            if (browser?.IsConnected == true)
                return browser;

            await CloseBrowserCoreAsync();

            string executablePath = GetChromiumExecutablePath();
            if (string.IsNullOrEmpty(executablePath))
            {
                Log.Warning("FlixCDN headed Chromium executable was not found");
                return null;
            }

            string display = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var started = await StartXvfbAsync(cancellationToken);
                xvfb = started.process;
                display = started.display;
                if (xvfb == null || string.IsNullOrEmpty(display))
                    return null;
            }

            playwright = await Playwright.CreateAsync();
            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = false,
                ExecutablePath = executablePath,
                Args = GetHeadedBrowserArgs()
            };

            if (!string.IsNullOrEmpty(display))
                launchOptions.Env = BuildEnvironment(display);

            browser = await playwright.Chromium.LaunchAsync(launchOptions);
            return browser;
        }
        catch (Exception ex)
        {
            Log.Warning("FlixCDN headed browser startup failed ({ErrorType})", ex.GetType().Name);
            await CloseBrowserCoreAsync();
            return null;
        }
        finally
        {
            lifecycleGate.Release();
        }
    }


    static async Task<(Process process, string display)> StartXvfbAsync(CancellationToken cancellationToken)
    {
        const string executable = "/usr/bin/Xvfb";
        if (!File.Exists(executable))
        {
            Log.Warning("FlixCDN requires Xvfb for headed Chromium on Linux");
            return default;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-displayfd");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-screen");
        startInfo.ArgumentList.Add("0");
        startInfo.ArgumentList.Add("1280x720x24");
        startInfo.ArgumentList.Add("-nolisten");
        startInfo.ArgumentList.Add("tcp");

        var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                process.Dispose();
                return default;
            }

            string displayNumber = await process.StandardOutput.ReadLineAsync()
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            if (process.HasExited || !int.TryParse(displayNumber, out int number) || number < 0)
            {
                StopProcess(process);
                return default;
            }

            return (process, $":{number}");
        }
        catch
        {
            StopProcess(process);
            throw;
        }
    }


    static Dictionary<string, string> BuildEnvironment(string display)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
        {
            if (item.Key is string key && item.Value is string value)
                environment[key] = value;
        }

        environment["DISPLAY"] = display;
        return environment;
    }


    static string GetChromiumExecutablePath()
    {
        string configured = CoreInit.conf.chromium.executablePath;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        string[] candidates;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            candidates = [".playwright/chrome-linux/chrome", "/usr/bin/chromium", "/usr/bin/chromium-browser"];
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            candidates =
            [
                ".playwright/chrome-mac/Chromium.app/Contents/MacOS/Chromium",
                "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
            ];
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            candidates = [".playwright\\chrome-win\\chrome.exe", ".playwright\\chrome-win32\\chrome.exe"];
        }
        else
        {
            return null;
        }

        return candidates.FirstOrDefault(File.Exists);
    }


    static string[] GetHeadedBrowserArgs()
    {
        var args = CoreInit.conf.chromium.Args;
        if (args == null || args.Length == 0)
            return args;

        return args.Where(arg =>
            !string.Equals(arg, "--headless", StringComparison.OrdinalIgnoreCase)
            && !(arg?.StartsWith("--headless=", StringComparison.OrdinalIgnoreCase) ?? false)
        ).ToArray();
    }


    static bool IsPlaybackResponse(IResponse response, string authority, int id, int translation, short season, short episode)
    {
        if (!Uri.TryCreate(response.Url, UriKind.Absolute, out var uri)
            || uri.AbsolutePath != "/api/player/files"
            || !string.Equals(uri.Authority, authority, StringComparison.OrdinalIgnoreCase))
            return false;

        return MatchesPlaybackRequest(response.Request, id, translation, season, episode);
    }


    static bool MatchesPlaybackRequest(IRequest request, int id, int translation, short season, short episode)
    {
        if (request.Method != "POST" || string.IsNullOrEmpty(request.PostData))
            return false;

        try
        {
            using var json = JsonDocument.Parse(request.PostData);
            int Number(string name) => json.RootElement.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : 0;

            return Number("id") == id && Number("translation") == translation
                && Number("season_number") == season && Number("episode_number") == episode;
        }
        catch (JsonException)
        {
            return false;
        }
    }


    static void CancelIdleShutdown()
    {
        lock (idleSync)
            idleTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }


    static void ScheduleIdleShutdown()
    {
        lock (idleSync)
        {
            idleTimer ??= new Timer(_ => _ = CloseIfIdleAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            idleTimer.Change(idleLifetime, Timeout.InfiniteTimeSpan);
        }
    }


    static async Task CloseIfIdleAsync()
    {
        if (Volatile.Read(ref activeRequests) != 0)
            return;

        await lifecycleGate.WaitAsync();
        try
        {
            if (Volatile.Read(ref activeRequests) == 0)
                await CloseBrowserCoreAsync();
        }
        finally
        {
            lifecycleGate.Release();
        }
    }


    static async Task CloseBrowserCoreAsync()
    {
        var currentBrowser = browser;
        browser = null;
        if (currentBrowser != null)
        {
            try { await currentBrowser.CloseAsync(); }
            catch { }
        }

        playwright?.Dispose();
        playwright = null;

        var currentXvfb = xvfb;
        xvfb = null;
        StopProcess(currentXvfb);
    }


    static void StopProcess(Process process)
    {
        if (process == null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch { }
        finally
        {
            process.Dispose();
        }
    }


    public static void Dispose()
    {
        lock (idleSync)
        {
            idleTimer?.Dispose();
            idleTimer = null;
        }

        lifecycleGate.Wait();
        try { CloseBrowserCoreAsync().GetAwaiter().GetResult(); }
        finally { lifecycleGate.Release(); }
    }
}

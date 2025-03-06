using Lampac;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
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
                if (!AppInit.conf.chromium.enable || browser != null || shutdown)
                    return;

                string executablePath = AppInit.conf.chromium.executablePath;

                // donwload


                if (string.IsNullOrEmpty(executablePath))
                    return;

                var playwright = await Playwright.CreateAsync();

                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = AppInit.conf.chromium.Headless,
                    ExecutablePath = executablePath
                });

                Status = AppInit.conf.chromium.Headless ? ChromiumStatus.headless : ChromiumStatus.NoHeadless;
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

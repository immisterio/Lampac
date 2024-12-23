﻿using Lampac;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Engine
{
    public class PuppeteerTo : IDisposable
    {
        #region static
        static IBrowser browser_keepopen = null;

        static DateTime exLifetime = default;

        static bool shutdown = false;

        public static bool IsKeepOpen => AppInit.conf.multiaccess || AppInit.conf.puppeteer.keepopen;

        static PuppeteerTo()
        {
            ThreadPool.QueueUserWorkItem(async _ =>
            {
                while (!shutdown)
                {
                    await Task.Delay(TimeSpan.FromMinutes(2)).ConfigureAwait(false);

                    try
                    {
                        if (!IsKeepOpen || exLifetime == default || browser_keepopen == null)
                            continue;

                        if (DateTime.Now > exLifetime)
                            await browser_keepopen.DisposeAsync();
                    }
                    catch { }
                }
            });
        }

        public static void LaunchKeepOpen()
        {
            browser_keepopen = Launch()?.Result;

            if (browser_keepopen != null)
            {
                exLifetime = DateTime.Now.AddMinutes(15);
                browser_keepopen.Closed += Browser_keepopen_Closed;
            }
        }

        async private static void Browser_keepopen_Closed(object sender, EventArgs e)
        {
            if (browser_keepopen != null)
                browser_keepopen.Closed -= Browser_keepopen_Closed;

            await Task.Delay(2_000);
            browser_keepopen = await Launch();

            if (browser_keepopen != null)
            {
                exLifetime = DateTime.Now.AddMinutes(15);
                browser_keepopen.Closed += Browser_keepopen_Closed;
            }
        }

        async public static ValueTask<PuppeteerTo> Browser()
        {
            try
            {
                if (shutdown)
                    return null;

                if (IsKeepOpen && browser_keepopen == null)
                    LaunchKeepOpen();

                if (browser_keepopen != null)
                    return new PuppeteerTo(browser_keepopen);

                return new PuppeteerTo(await Launch());
            }
            catch { return null; }
        }

        static Task<IBrowser> Launch()
        {
            if (!AppInit.conf.puppeteer.enable || shutdown)
                return null;

            try
            {
                var option = new LaunchOptions()
                {
                    Headless = AppInit.conf.puppeteer.Headless,
                    Devtools = false,
                    IgnoreHTTPSErrors = true,
                    Args = new string[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage", "--disable-gpu", "--renderer-process-limit=1" },
                    Timeout = 12_000
                };

                if (!string.IsNullOrEmpty(AppInit.conf.puppeteer.executablePath))
                    option.ExecutablePath = AppInit.conf.puppeteer.executablePath;

                return Puppeteer.LaunchAsync(option);
            }
            catch (Exception ex) 
            { 
                Console.WriteLine(ex.ToString()); 
                return null; 
            }
        }
        #endregion


        IBrowser browser;

        IPage page;

        public PuppeteerTo(IBrowser browser)
        {
            this.browser = browser; 
        }

        public ValueTask<IPage> Page(Dictionary<string, string> headers = null)
        {
            return Page(null, headers);
        }

        async public ValueTask<IPage> Page(CookieParam[] cookies, Dictionary<string, string> headers = null)
        {
            try
            {
                if (browser == null)
                    return null;

                page = IsKeepOpen ? await browser.NewPageAsync() : (await browser.PagesAsync())[0];

                await page.SetCacheEnabledAsync(IsKeepOpen);
                await page.DeleteCookieAsync();

                if (headers != null && headers.Count > 0)
                    await page.SetExtraHttpHeadersAsync(headers);

                if (cookies != null)
                    await page.SetCookieAsync(cookies);

                await page.SetRequestInterceptionAsync(true);
                page.Request += Page_Request;

                return page;
            }
            catch { return null; }
        }

        async public ValueTask<IPage> MainPage()
        {
            try
            {
                if (browser == null)
                    return null;

                return (await browser.PagesAsync())[0];
            }
            catch { return null; }
        }

        private void Page_Request(object sender, RequestEventArgs e)
        {
            try
            {
                if (e?.Request == null)
                    return;

                if (Regex.IsMatch(e.Request.Url, "\\.(ico|png|jpe?g|WEBP|svg|css|EOT|TTF|WOFF2?|OTF)", RegexOptions.IgnoreCase) || e.Request.Url.StartsWith("data:image"))
                {
                    e.Request.AbortAsync();
                    return;
                }

                e.Request.ContinueAsync();
            }
            catch { }
        }

        public void Dispose()
        {
            if (browser == null || !AppInit.conf.puppeteer.Headless)
                return;

            try
            {
                if (!IsKeepOpen)
                {
                    browser.CloseAsync().Wait();
                    browser.Dispose();
                }
                else if (page != null)
                {
                    page.Request -= Page_Request;
                    page.CloseAsync();
                    page.Dispose();
                }
            }
            catch { }
        }

        public static void FullDispose()
        {
            shutdown = true;
            if (browser_keepopen == null)
                return;

            try
            {
                browser_keepopen.CloseAsync().Wait(2000);
                browser_keepopen.Dispose();
            }
            catch { }
        }
    }
}

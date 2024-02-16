using Lampac;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Shared.Engine
{
    public class PuppeteerTo : IDisposable
    {
        #region static
        static IBrowser browser_keepopen = null;

        async public static ValueTask<PuppeteerTo> Browser()
        {
            if (browser_keepopen != null)
                return new PuppeteerTo(browser_keepopen);

            bool isdev = File.Exists(@"C:\ProgramData\lampac\disablesync");

            var b = await Puppeteer.LaunchAsync(new LaunchOptions()
            {
                Headless = !isdev, /*false*/
                Devtools = isdev,
                IgnoreHTTPSErrors = true,
                Args = new string[] { "--no-sandbox --disable-setuid-sandbox --disable-dev-shm-usage --disable-gpu --renderer-process-limit=1" },
                Timeout = 15_000
            });

            if (AppInit.conf.puppeteer_keepopen)
                browser_keepopen = b;

            return new PuppeteerTo(b);
        }
        #endregion

        IBrowser browser;

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
            var page = (await browser.PagesAsync())[0];

            if (headers != null && headers.Count > 0)
                await page.SetExtraHttpHeadersAsync(headers);

            await page.SetCacheEnabledAsync(AppInit.conf.puppeteer_keepopen);
            await page.DeleteCookieAsync();

            if (cookies != null)
                await page.SetCookieAsync(cookies);

            return page;
        }

        public void Dispose()
        {
            if (!AppInit.conf.puppeteer_keepopen)
                browser.Dispose();
            else
            {
                var pages = browser.PagesAsync().Result;

                foreach (var pg in pages.Skip(1))
                    pg.CloseAsync();

                pages[0].GoToAsync("about:blank");
            }
        }
    }
}

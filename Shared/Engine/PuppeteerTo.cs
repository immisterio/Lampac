using PuppeteerSharp;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared.Engine
{
    public static class PuppeteerTo
    {
        public static Task<IBrowser> Browser()
        {
            return Puppeteer.LaunchAsync(new LaunchOptions()
            {
                Headless = true, /*false*/
                IgnoreHTTPSErrors = true,
                Args = new string[] { "--no-sandbox --disable-setuid-sandbox --disable-dev-shm-usage --disable-gpu --renderer-process-limit=1" },
                Timeout = 10_000
            });
        }

        async public static ValueTask<IPage> Page(IBrowser browser, Dictionary<string, string> headers = null)
        {
            var page = (await browser.PagesAsync())[0];

            if (headers != null && headers.Count > 0)
                await page.SetExtraHttpHeadersAsync(headers);

            await page.SetCacheEnabledAsync(false);

            return page;
        }
    }
}

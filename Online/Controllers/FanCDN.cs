using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using Shared.Model.Online.FanCDN;
using Shared.Engine.Online;
using Shared.Engine;
using Lampac.Models.LITE;
using Microsoft.Playwright;
using Shared.Engine.CORE;
using System.Net;

namespace Lampac.Controllers.LITE
{
    public class FanCDN : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/fancdn")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int year, int t = -1, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.FanCDN);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (Chromium.Status != ChromiumStatus.NoHeadless)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            var oninvk = new FanCDNInvoke
            (
               host,
               init.host,
               ongettourl => black_magic(ongettourl, init, proxy.data),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy.proxy)
            );

            var cache = await InvokeCache<EmbedModel>($"fancdn:{kinopoisk_id}:{imdb_id}", cacheTime(20, init: init), null, async res =>
            {
                return await oninvk.Embed(null, imdb_id, kinopoisk_id);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, imdb_id, kinopoisk_id, title, original_title, t, s, rjson: rjson, vast: init.vast), origsource: origsource);
        }


        async ValueTask<string> black_magic(string uri, OnlinesSettings init, (string ip, string username, string password) proxy)
        {
            try
            {
                using (var browser = new Chromium())
                {
                    var page = await browser.NewPageAsync(proxy: proxy);
                    if (page == null)
                        return null;

                    await page.Context.ClearCookiesAsync(new BrowserContextClearCookiesOptions { Domain = ".fancdn.net", Name = "cf_clearance" });

                    await page.RouteAsync("**/*", async route =>
                    {
                        if (route.Request.Url.Contains("api/chromium/iframe"))
                        {
                            await route.ContinueAsync();
                            return;
                        }

                        if (route.Request.Url == uri)
                        {
                            string html = null;
                            await route.ContinueAsync(new RouteContinueOptions { Headers = httpHeaders(init).ToDictionary() });

                            var response = await page.WaitForResponseAsync(route.Request.Url);
                            if (response != null)
                                html = await response.TextAsync();

                            browser.completionSource.SetResult(html);
                            Chromium.WebLog(route.Request, response, html);
                            return;
                        }

                        await route.AbortAsync();
                    });

                    var response = await page.GotoAsync(Chromium.IframeUrl(uri));
                    if (response == null)
                        return null;

                    return await browser.WaitPageResult();
                }
            }
            catch { return null; }
        }
    }
}

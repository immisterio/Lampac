using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using Shared.Model.Online.FanCDN;
using Shared.Engine.Online;
using Shared.Engine;
using Lampac.Models.LITE;
using Microsoft.Playwright;
using Shared.Engine.CORE;
using System;
using Shared.PlaywrightCore;

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

            if (kinopoisk_id == 0)
                return OnError();

            if (PlaywrightBrowser.Status != PlaywrightStatus.NoHeadless)
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

            var cache = await InvokeCache<EmbedModel>($"fancdn:{kinopoisk_id}:{proxyManager.CurrentProxyIp}", cacheTime(20, init: init), proxyManager, async res =>
            {
                var result = await oninvk.Embed(null, null, kinopoisk_id);
                if (result == null)
                    return res.Fail(logRequest);

                return result;
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, imdb_id, kinopoisk_id, title, original_title, t, s, rjson: rjson, vast: init.vast), origsource: origsource);
        }


        string logRequest = string.Empty;

        async ValueTask<string> black_magic(string uri, OnlinesSettings init, (string ip, string username, string password) proxy)
        {
            try
            {
                using (var browser = new PlaywrightBrowser())
                {
                    var page = await browser.NewPageAsync(proxy: proxy);
                    if (page == null)
                    {
                        logRequest += "\nNewPageAsync null";
                        return null;
                    }

                    await page.Context.ClearCookiesAsync(new BrowserContextClearCookiesOptions { Domain = ".fancdn.net", Name = "cf_clearance" });

                    page.RequestFailed += (sender, e) =>
                    {
                        if (e.Url == uri)
                        {
                            browser.completionSource.SetResult(null);
                            PlaywrightBase.WebLog(e.Method, e.Url, "RequestFailed", proxy, e);
                        }
                    };

                    await page.RouteAsync("**/*", async route =>
                    {
                        if (route.Request.Url.Contains("api/chromium/iframe"))
                        {
                            await route.ContinueAsync();
                            return;
                        }

                        logRequest += $"{route.Request.Method}: {route.Request.Url}\n";

                        if (route.Request.Url == uri)
                        {
                            string html = null;
                            await route.ContinueAsync(new RouteContinueOptions { Headers = httpHeaders(init).ToDictionary() });

                            var response = await page.WaitForResponseAsync(route.Request.Url);
                            if (response != null)
                                html = await response.TextAsync();

                            browser.completionSource.SetResult(html);
                            PlaywrightBase.WebLog(route.Request, response, html, proxy);
                            return;
                        }

                        await route.AbortAsync();
                    });

                    var response = await page.GotoAsync(PlaywrightBase.IframeUrl(uri));
                    if (response == null)
                        return null;

                    return await browser.WaitPageResult();
                }
            }
            catch (Exception ex) 
            {
                logRequest += $"\n{ex.Message}";
                return null; 
            }
        }
    }
}

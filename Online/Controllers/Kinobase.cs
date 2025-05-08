using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Online;
using Shared.Engine.CORE;
using Shared.Model.Online.Kinobase;
using Microsoft.Playwright;
using Shared.Engine;
using Shared.PlaywrightCore;
using Lampac.Models.LITE;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Lampac.Controllers.LITE
{
    public class Kinobase : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinobase")]
        async public Task<ActionResult> Index(string title, int year, int s = -1, int serial = -1, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Kinobase);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(title) || year == 0)
                return OnError();

            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(AppInit.conf.Kinobase);
            var proxy = proxyManager.BaseGet();

            var oninvk = new KinobaseInvoke
            (
               host,
               init.corsHost(),
               ongettourl => 
               {
                   if (ongettourl.Contains("/search?query="))
                       return HttpClient.Get(ongettourl, timeoutSeconds: 8, proxy: proxy.proxy, referer: init.host, httpversion: 2, headers: httpHeaders(init));

                   return black_magic(ongettourl, init, proxy.data);
               },
               (url, data) => HttpClient.Post(url, data, timeoutSeconds: 8, proxy: proxy.proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy.proxy),
               requesterror: () => proxyManager.Refresh()
            );

            var cache = await InvokeCache<EmbedModel>($"kinobase:view:{title}:{year}:{proxyManager.CurrentProxyIp}", cacheTime(20, init: init), proxyManager, async res =>
            {
                var content = await oninvk.Embed(title, year);
                if (content == null)
                    return res.Fail("embed");

                return content;
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, title, year, s, rjson));
        }



        #region black_magic
        async ValueTask<string> black_magic(string uri, OnlinesSettings init, (string ip, string username, string password) proxy)
        {
            try
            {
                using (var browser = new PlaywrightBrowser())
                {
                    var page = await browser.NewPageAsync(init.plugin, proxy: proxy, headers: init.headers);
                    if (page == null)
                        return null;

                    await page.Context.AddCookiesAsync(new List<Cookie>()
                    {
                        new Cookie()
                        {
                            Name = "player_settings",
                            Value = "old|hls|0",
                            Domain = Regex.Match(init.host, "^https?://([^/]+)").Groups[1].Value,
                            Path = "/",
                            Expires = 2220002226
                        }
                    });

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            if (route.Request.Url.Contains("/uppod.js"))
                            {
                                await route.FulfillAsync(new RouteFulfillOptions
                                {
                                    Body = System.IO.File.ReadAllText("data/kinobase_uppod.js")
                                });

                                return;
                            }

                            if (!route.Request.Url.Contains(init.host) || route.Request.Url.Contains("/comments"))
                            {
                                await route.AbortAsync();
                                return;
                            }

                            if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                return;

                            await route.ContinueAsync();
                        }
                        catch { }
                    });

                    PlaywrightBase.GotoAsync(page, uri);
                    await page.WaitForSelectorAsync(".uppod-media");
                    string content = await page.ContentAsync();

                    PlaywrightBase.WebLog("GET", uri, content, proxy);
                    return content;
                }
            }
            catch { return null; }
        }
        #endregion
    }
}

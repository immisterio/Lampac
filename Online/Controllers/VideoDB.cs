using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.Online;
using Online;
using Shared.Model.Online.VideoDB;
using Microsoft.Extensions.Caching.Memory;
using Shared.Model.Templates;
using Shared.Engine;
using Lampac.Models.LITE;
using Microsoft.Playwright;
using Shared.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class VideoDB : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/videodb")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, string t, int s = -1, int sid = -1, bool origsource = false, bool rjson = false, int serial = -1)
        {
            var init = await loadKit(AppInit.conf.VideoDB);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (kinopoisk_id == 0 || Chromium.Status != ChromiumStatus.NoHeadless)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            var oninvk = new VideoDBInvoke
            (
               host,
               init.host,
               (url, head) => black_magic(url, init, proxy.data),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy.proxy)
            );

            var cache = await InvokeCache<EmbedModel>($"videodb:view:{kinopoisk_id}", cacheTime(20, init: init), null, async res =>
            {
                return await oninvk.Embed(kinopoisk_id);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, accsArgs(string.Empty), kinopoisk_id, title, original_title, t, s, sid, rjson), origsource: origsource);
        }


        [HttpGet]
        [Route("lite/videodb/manifest")]
        [Route("lite/videodb/manifest.m3u8")]
        async public Task<ActionResult> Manifest(string link, bool serial)
        {
            var init = await loadKit(AppInit.conf.VideoDB);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(link) || Chromium.Status != ChromiumStatus.NoHeadless)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string memKey = $"videodb:video:{link}";
            if (!memoryCache.TryGetValue(memKey, out string location))
            {
                try
                {
                    using (var browser = new Chromium())
                    {
                        var page = await browser.NewPageAsync(proxy: proxy.data);
                        if (page == null)
                            return null;

                        await page.RouteAsync("**/*", async route =>
                        {
                            if (route.Request.Url.Contains("api/chromium/iframe"))
                            {
                                await route.ContinueAsync();
                                return;
                            }

                            if (route.Request.Url == link)
                            {
                                await route.ContinueAsync(new RouteContinueOptions { Headers = httpHeaders(init).ToDictionary() });

                                var response = await page.WaitForResponseAsync(route.Request.Url);
                                if (response != null)
                                    response.Headers.TryGetValue("location", out location);

                                browser.completionSource.SetResult(location);
                                Chromium.WebLog(route.Request, response, location);
                                return;
                            }

                            await route.AbortAsync();
                        });

                        var response = await page.GotoAsync(Chromium.IframeUrl(link));
                        if (response == null)
                            return null;

                        location = await browser.WaitPageResult();
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(location) || link == location)
                    return OnError();

                memoryCache.Set(memKey, location, cacheTime(20, rhub: 2, init: init));
            }

            string hls = HostStreamProxy(init, location, proxy: proxy.proxy);

            if (HttpContext.Request.Path.Value.Contains(".m3u8"))
                return Redirect(hls);

            return ContentTo(VideoTpl.ToJson("play", hls, "auto", vast: init.vast));
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

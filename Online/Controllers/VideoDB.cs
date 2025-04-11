using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.Online;
using Online;
using Shared.Model.Online.VideoDB;
using Shared.Model.Templates;
using Shared.Engine;
using Lampac.Models.LITE;
using Microsoft.Playwright;
using Shared.Engine.CORE;
using Shared.PlaywrightCore;
using System;
using Lampac.Engine.CORE;
using System.Net;

namespace Lampac.Controllers.LITE
{
    public class VideoDB : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/videodb")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, string t, int s = -1, int sid = -1, bool origsource = false, bool rjson = false, int serial = -1)
        {
            var init = await loadKit(AppInit.conf.VideoDB);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (kinopoisk_id == 0)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: serial == 0 ? null : -1);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            var oninvk = new VideoDBInvoke
            (
               host,
               init.host,
               (url, head) => rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : black_magic(url, init, proxy),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy.proxy)
            );

            var cache = await InvokeCache<EmbedModel>(rch.ipkey($"videodb:view:{kinopoisk_id}", proxyManager), cacheTime(20, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(kinopoisk_id);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, accsArgs(string.Empty), kinopoisk_id, title, original_title, t, s, sid, rjson), origsource: origsource);
        }


        #region Manifest
        [HttpGet]
        [Route("lite/videodb/manifest")]
        [Route("lite/videodb/manifest.m3u8")]
        async public Task<ActionResult> Manifest(string link, bool serial)
        {
            var init = await loadKit(AppInit.conf.VideoDB);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(link))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: serial ? -1 : null);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            string memKey = rch.ipkey($"videodb:video:{link}", proxyManager);
            if (!hybridCache.TryGetValue(memKey, out string location))
            {
                try
                {
                    if (rch.enable)
                    {
                        if (rch.IsNotConnected())
                            return ContentTo(rch.connectionMsg);

                        var res = await rch.Headers(link, null, httpHeaders(init));
                        location = res.currentUrl;
                    }
                    else if (init.priorityBrowser == "http")
                    {
                        location = await HttpClient.GetLocation(link, httpversion: 2, timeoutSeconds: 8, proxy: proxy.proxy, headers: httpHeaders(init));
                    }
                    else
                    {
                        using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                        {
                            var page = await browser.NewPageAsync(init.plugin, proxy: proxy.data);
                            if (page == null)
                                return null;

                            browser.failedUrl = link;

                            await page.RouteAsync("**/*", async route =>
                            {
                                try
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

                                        browser.SetPageResult(location);
                                        PlaywrightBase.WebLog(route.Request, response, location, proxy.data);
                                        return;
                                    }

                                    await route.AbortAsync();
                                }
                                catch { }
                            });

                            var response = await page.GotoAsync(PlaywrightBase.IframeUrl(link));
                            if (response == null)
                                return null;

                            location = await browser.WaitPageResult();
                        }
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(location) || link == location)
                {
                    if (init.rhub && init.rhub_fallback)
                    {
                        init.rhub = false;
                        goto reset;
                    }
                    return OnError();
                }

                if (!rch.enable)
                    proxyManager.Success();

                hybridCache.Set(memKey, location, cacheTime(20, rhub: 2, init: init));
            }

            string hls = HostStreamProxy(init, location, proxy: proxy.proxy);

            if (HttpContext.Request.Path.Value.Contains(".m3u8"))
                return Redirect(hls);

            return ContentTo(VideoTpl.ToJson("play", hls, "auto", vast: init.vast));
        }
        #endregion

        #region black_magic
        async ValueTask<string> black_magic(string uri, OnlinesSettings init, (WebProxy proxy, (string ip, string username, string password) data) baseproxy)
        {
            try
            {
                if (init.priorityBrowser == "http")
                    return await HttpClient.Get(uri, httpversion: 2, timeoutSeconds: 8, proxy: baseproxy.proxy, headers: httpHeaders(init));

                using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                {
                    var page = await browser.NewPageAsync(init.plugin, proxy: baseproxy.data);
                    if (page == null)
                        return null;

                    browser.failedUrl = uri;

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            if (route.Request.Url.Contains("api/chromium/iframe"))
                            {
                                await route.ContinueAsync();
                                return;
                            }

                            if (browser.IsCompleted)
                            {
                                Console.WriteLine($"Playwright: Abort {route.Request.Url}");
                                await route.AbortAsync();
                                return;
                            }

                            if (route.Request.Url == uri)
                            {
                                string html = null;
                                await route.ContinueAsync(new RouteContinueOptions { Headers = httpHeaders(init).ToDictionary() });

                                var response = await page.WaitForResponseAsync(route.Request.Url);
                                if (response != null)
                                    html = await response.TextAsync();

                                browser.SetPageResult(html);
                                PlaywrightBase.WebLog(route.Request, response, html, baseproxy.data);
                                return;
                            }

                            await route.AbortAsync();
                        }
                        catch { }
                    });

                    var response = await page.GotoAsync(PlaywrightBase.IframeUrl(uri));
                    if (response == null)
                        return null;

                    return await browser.WaitPageResult();
                }
            }
            catch { return null; }
        }
        #endregion
    }
}

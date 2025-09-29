using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Shared.Models.Online.Settings;
using Shared.Models.Online.VideoDB;
using Shared.PlaywrightCore;
using System.Net;

namespace Online.Controllers
{
    public class VideoDB : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/videodb")]
        async public ValueTask<ActionResult> Index(long kinopoisk_id, string title, string original_title, string t, int s = -1, int sid = -1, bool origsource = false, bool rjson = false, int serial = -1)
        {
            var init = await loadKit(AppInit.conf.VideoDB);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (kinopoisk_id == 0)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: serial == 0 ? null : -1);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            var oninvk = new VideoDBInvoke
            (
               host,
               init.apihost,
               (url, head) => rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : black_magic(url, init, proxy),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy.proxy)
            );

            reset: 
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
        async public ValueTask<ActionResult> Manifest(string link, bool serial)
        {
            var init = await loadKit(AppInit.conf.VideoDB);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(link))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: serial ? -1 : null);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            string memKey = rch.ipkey($"videodb:video:{link}", proxyManager);

            return await InvkSemaphore(init, memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out string location))
                {
                    reset:

                    try
                    {
                        var headers = httpHeaders(init, HeadersModel.Init(
                            ("sec-fetch-dest", "empty"),
                            ("sec-fetch-mode", "cors"),
                            ("sec-fetch-site", "same-site"),
                            ("origin", "{host}"),
                            ("referer", "{host}/")
                        ));

                        if (rch.enable)
                        {
                            if (rch.IsNotConnected())
                                return ContentTo(rch.connectionMsg);

                            var res = await rch.Headers(link, null, headers);
                            location = res.currentUrl;
                        }
                        else if (init.priorityBrowser == "http")
                        {
                            location = await Http.GetLocation(link, httpversion: 2, timeoutSeconds: 8, proxy: proxy.proxy, headers: headers);
                        }
                        else
                        {
                            using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                            {
                                var page = await browser.NewPageAsync(init.plugin, proxy: proxy.data);
                                if (page == null)
                                    return null;

                                browser.SetFailedUrl(link);

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
                                            await route.ContinueAsync(new RouteContinueOptions { Headers = headers.ToDictionary() });

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

                                PlaywrightBase.GotoAsync(page, PlaywrightBase.IframeUrl(link));

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
                    return RedirectToPlay(hls);

                return ContentTo(VideoTpl.ToJson("play", hls, "auto", vast: init.vast));
            });
        }
        #endregion

        #region black_magic
        async ValueTask<string> black_magic(string uri, OnlinesSettings init, (WebProxy proxy, (string ip, string username, string password) data) baseproxy)
        {
            try
            {
                var headers = httpHeaders(init, HeadersModel.Init(
                    ("sec-fetch-dest", "iframe"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "cross-site"),
                    ("referer", "{host}/")
                ));

                if (init.priorityBrowser == "http")
                    return await Http.Get(uri, httpversion: 2, timeoutSeconds: 8, proxy: baseproxy.proxy, headers: headers);

                using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                {
                    var page = await browser.NewPageAsync(init.plugin, init.headers, proxy: baseproxy.data, imitationHuman: init.imitationHuman).ConfigureAwait(false);
                    if (page == null)
                        return null;

                    browser.SetFailedUrl(uri);

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            if (route.Request.Url.StartsWith(init.host))
                            {
                                await route.FulfillAsync(new RouteFulfillOptions
                                {
                                    Body = PlaywrightBase.IframeHtml(uri)
                                });
                            }
                            else if (route.Request.Url == uri)
                            {
                                string html = null;
                                await route.ContinueAsync();

                                var response = await page.WaitForResponseAsync(route.Request.Url);
                                if (response != null)
                                    html = await response.TextAsync();

                                browser.SetPageResult(html);
                                PlaywrightBase.WebLog(route.Request, response, html, baseproxy.data);
                                return;
                            }
                            else
                            {
                                if (!init.imitationHuman || route.Request.Url.EndsWith(".m3u8") || route.Request.Url.Contains("/cdn-cgi/challenge-platform/"))
                                {
                                    PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                }
                                else
                                {
                                    if (await PlaywrightBase.AbortOrCache(page, route))
                                        return;

                                    await route.ContinueAsync();
                                }
                            }
                        }
                        catch { }
                    });

                    PlaywrightBase.GotoAsync(page, init.host);

                    return await browser.WaitPageResult().ConfigureAwait(false);
                }
            }
            catch { return null; }
        }
        #endregion
    }
}

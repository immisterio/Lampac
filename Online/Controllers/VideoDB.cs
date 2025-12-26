using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class VideoDB : BaseOnlineController
    {
        public VideoDB() : base(AppInit.conf.VideoDB) { }

        [HttpGet]
        [Route("lite/videodb")]
        async public ValueTask<ActionResult> Index(long kinopoisk_id, string title, string original_title, string t, int s = -1, int sid = -1, bool rjson = false, int serial = -1)
        {
            if (kinopoisk_id == 0)
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            var oninvk = new VideoDBInvoke
            (
               host,
               init.apihost,
               (url, head) => black_magic(url),
               () => proxyManager.Refresh(rch)
            );

            rhubFallback: 
            var cache = await InvokeCacheResult(rch.ipkey($"videodb:view:{kinopoisk_id}", proxyManager), 20, 
                () => oninvk.Embed(kinopoisk_id)
            );

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return OnResult(cache, () => oninvk.Tpl(cache.Value, accsArgs(string.Empty), kinopoisk_id, title, original_title, t, s, sid, rjson));
        }


        #region Manifest
        [HttpGet]
        [Route("lite/videodb/manifest")]
        [Route("lite/videodb/manifest.m3u8")]
        async public ValueTask<ActionResult> Manifest(string link, bool serial)
        {
            if (string.IsNullOrEmpty(link))
                return OnError();

            if (await IsRequestBlocked(rch: true, rch_check: false))
                return badInitMsg;

            bool play = HttpContext.Request.Path.Value.Contains(".m3u8");

            if (rch.IsNotConnected())
            {
                if (init.rhub_fallback && play)
                    rch.Disabled();
                else
                    return ContentTo(rch.connectionMsg);
            }

            if (!play && rch.IsRequiredConnected())
                return ContentTo(rch.connectionMsg);

            if (rch.IsNotSupport(out string rch_error))
                return ShowError(rch_error);

            return await InvkSemaphore($"videodb:video:{link}", async key =>
            {
                reset:
                string memKey = rch.ipkey(key, proxyManager);
                if (!hybridCache.TryGetValue(memKey, out string location))
                {
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
                            var res = await rch.Headers(link, null, headers);
                            location = res.currentUrl;
                        }
                        else if (init.priorityBrowser == "http")
                        {
                            location = await Http.GetLocation(link, httpversion: init.httpversion, timeoutSeconds: init.httptimeout, proxy: proxy, headers: headers);
                        }
                        else
                        {
                            using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                            {
                                var page = await browser.NewPageAsync(init.plugin, proxy: proxy_data);
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
                                            PlaywrightBase.WebLog(route.Request, response, location, proxy_data);
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

                    hybridCache.Set(memKey, location, cacheTimeBase(20, rhub: 2, init: init));
                }

                string hls = HostStreamProxy(location);

                if (play)
                    return RedirectToPlay(hls);

                return ContentTo(VideoTpl.ToJson("play", hls, "auto", vast: init.vast));
            });
        }
        #endregion

        #region black_magic
        async Task<string> black_magic(string iframe_uri)
        {
            try
            {
                var headers = httpHeaders(init, HeadersModel.Init(
                    ("sec-fetch-dest", "iframe"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "cross-site"),
                    ("referer", "{host}/")
                ));

                if (rch.enable || init.priorityBrowser == "http")
                    return await httpHydra.Get(iframe_uri, newheaders: headers);

                using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                {
                    var page = await browser.NewPageAsync(init.plugin, init.headers, proxy: proxy_data, imitationHuman: init.imitationHuman).ConfigureAwait(false);
                    if (page == null)
                        return null;

                    browser.SetFailedUrl(iframe_uri);

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            if (route.Request.Url.StartsWith(init.host))
                            {
                                await route.FulfillAsync(new RouteFulfillOptions
                                {
                                    Body = PlaywrightBase.IframeHtml(iframe_uri)
                                });
                            }
                            else if (route.Request.Url == iframe_uri)
                            {
                                string html = null;
                                await route.ContinueAsync();

                                var response = await page.WaitForResponseAsync(route.Request.Url);
                                if (response != null)
                                    html = await response.TextAsync();

                                browser.SetPageResult(html);
                                PlaywrightBase.WebLog(route.Request, response, html, proxy_data);
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

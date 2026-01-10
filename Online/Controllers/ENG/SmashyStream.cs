using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class SmashyStream : BaseENGController
    {
        public SmashyStream() : base(AppInit.conf.Smashystream) { }

        [HttpGet]
        [Route("lite/smashystream")]
        public Task<ActionResult> Index(bool checksearch, long id, long tmdb_id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(checksearch, id, tmdb_id, imdb_id, title, original_title, serial, s, rjson, hls_manifest_timeout: (int)TimeSpan.FromSeconds(35).TotalMilliseconds);
        }

        #region Video
        [HttpGet]
        [Route("lite/smashystream/video.m3u8")]
        async public ValueTask<ActionResult> Video(long id, int s = -1, int e = -1)
        {
            if (id == 0)
                return OnError();

            if (await IsRequestBlocked(rch: false, rch_check: false))
                return badInitMsg;

            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                return OnError();

            string embed = $"{init.host}/movie/{id}";
            if (s > 0)
                embed = $"{init.host}/tv/{id}?s={s}&e={e}";

            return await InvkSemaphore(embed, async () =>
            {
                var cache = await black_magic(embed);
                if (cache.m3u8 == null)
                    return StatusCode(502);

                return Redirect(HostStreamProxy(cache.m3u8, headers: cache.headers));
            });
        }
        #endregion

        #region black_magic
        async ValueTask<(string m3u8, List<HeadersModel> headers)> black_magic(string uri)
        {
            if (string.IsNullOrEmpty(uri))
                return default;

            try
            {
                string memKey = $"smashystream:black_magic:{uri}";
                if (!hybridCache.TryGetValue(memKey, out (string m3u8, List<HeadersModel> headers) cache))
                {
                    DateTime routeActiveTime = default;

                    using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                    {
                        var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy_data, deferredDispose: true).ConfigureAwait(false);
                        if (page == null)
                            return default;

                        await page.RouteAsync("**/*", async route =>
                        {
                            try
                            {
                                routeActiveTime = DateTime.Now;

                                if (browser.IsCompleted || route.Request.Url.Contains(".m3u") || Regex.IsMatch(route.Request.Url, "(\\.vtt|histats.com|solid.gif|poster.png|doubleclick\\.|inkblotconusor\\.|jrbbavbvqmrjw\\.)"))
                                {
                                    PlaywrightBase.ConsoleLog(() => $"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                    return;
                                }

                                if (await PlaywrightBase.AbortOrCache(page, route, fullCacheJS: true))
                                    return;

                                if (route.Request.Url.Contains("master."))
                                {
                                    cache.headers = new List<HeadersModel>();
                                    foreach (var item in route.Request.Headers)
                                    {
                                        if (item.Key.ToLower() is "host" or "accept-encoding" or "connection" or "range")
                                            continue;

                                        cache.headers.Add(new HeadersModel(item.Key, item.Value.ToString()));
                                    }

                                    PlaywrightBase.ConsoleLog(() => ($"Playwright: SET {route.Request.Url}", cache.headers));
                                    browser.SetPageResult(route.Request.Url);
                                    cache.m3u8 = route.Request.Url;
                                    await route.AbortAsync();
                                    return;
                                }

                                await route.ContinueAsync();
                            }
                            catch { }
                        });

                        await page.GotoAsync(uri, new PageGotoOptions() 
                        {
                            Timeout = 15_000,
                            WaitUntil = WaitUntilState.DOMContentLoaded 
                        }).ConfigureAwait(false);

                        var frameElement = await page.WaitForSelectorAsync("iframe[src*='smashyplayer.top']", new PageWaitForSelectorOptions 
                        { 
                            Timeout = 15_000, 
                            State = WaitForSelectorState.Visible 
                        }).ConfigureAwait(false);

                        var frame = await frameElement.ContentFrameAsync().ConfigureAwait(false);

                        // невьебенная магия обхода защиты =)
                        await Task.Delay(1000).ConfigureAwait(false);
                        while (routeActiveTime.AddSeconds(1) > DateTime.Now)
                            await Task.Delay(100).ConfigureAwait(false);

                        await frame.WaitForSelectorAsync("#player-button", new FrameWaitForSelectorOptions() { Timeout = 10_000 }).ConfigureAwait(false);

                        var endTime = DateTime.Now.AddSeconds(5);
                        while (endTime > DateTime.Now && cache.m3u8 == null)
                        {
                            try
                            {
                                await frame.ClickAsync("#player-button", new FrameClickOptions
                                {
                                    Timeout = 400,
                                    Force = true
                                }).ConfigureAwait(false);

                                await Task.Delay(200).ConfigureAwait(false);
                            }
                            catch { }
                        }
                    }

                    if (cache.m3u8 == null)
                    {
                        proxyManager?.Refresh();
                        return default;
                    }

                    proxyManager?.Success();
                    hybridCache.Set(memKey, cache, cacheTime(20));
                }

                return cache;
            }
            catch { return default; }
        }
        #endregion
    }
}

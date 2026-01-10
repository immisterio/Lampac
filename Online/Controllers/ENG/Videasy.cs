using Microsoft.AspNetCore.Mvc;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class Videasy : BaseENGController
    {
        public Videasy() : base(AppInit.conf.Videasy) { }

        [HttpGet]
        [Route("lite/videasy")]
        public Task<ActionResult> Index(bool checksearch, long id, long tmdb_id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(checksearch, id, tmdb_id, imdb_id, title, original_title, serial, s, rjson, method: "call");
        }

        #region Video
        [HttpGet]
        [Route("lite/videasy/video")]
        [Route("lite/videasy/video.m3u8")]
        async public ValueTask<ActionResult> Video(long id, int s = -1, int e = -1, bool play = false)
        {
            if (await IsRequestBlocked(rch: false, rch_check: !play))
                return badInitMsg;

            if (id == 0)
                return OnError();

            string embed = $"{init.host}/movie/{id}";
            if (s > 0)
                embed = $"{init.host}/tv/{id}/{s}/{e}";

            return await InvkSemaphore(embed, async () =>
            {
                var cache = await black_magic(embed);
                if (cache.m3u8 == null)
                    return StatusCode(502);

                var headers_stream = httpHeaders(init.host, init.headers_stream);
                if (headers_stream.Count == 0)
                    headers_stream = cache.headers;

                string hls = HostStreamProxy(cache.m3u8, headers: headers_stream);

                if (play)
                    return RedirectToPlay(hls);

                return ContentTo(VideoTpl.ToJson("play", hls, "English", vast: init.vast, headers: init.streamproxy ? null : headers_stream));
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
                string memKey = $"videasy:black_magic:{uri}";
                if (!hybridCache.TryGetValue(memKey, out (string m3u8, List<HeadersModel> headers) cache))
                {
                    using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                    {
                        var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy_data, deferredDispose: true);
                        if (page == null)
                            return default;

                        await page.RouteAsync("**/*", async route =>
                        {
                            try
                            {
                                if (browser.IsCompleted)
                                {
                                    PlaywrightBase.ConsoleLog(() => $"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                    return;
                                }

                                if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true))
                                    return;

                                if (route.Request.Url.Contains(".m3u8") || route.Request.Url.Contains(".mp4") || route.Request.Url.Contains("/mp4/") || route.Request.Url.Contains("mp4."))
                                {
                                    cache.headers = new List<HeadersModel>();
                                    foreach (var item in route.Request.Headers)
                                    {
                                        if (item.Key.ToLower() is "host" or "accept-encoding" or "connection" or "range")
                                            continue;

                                        cache.headers.Add(new HeadersModel(item.Key, item.Value.ToString()));
                                    }

                                    PlaywrightBase.ConsoleLog(() => ($"Playwright: SET {route.Request.Url}", cache.headers));
                                    browser.completionSource.SetResult(route.Request.Url);
                                    await route.AbortAsync();
                                    return;
                                }

                                await route.ContinueAsync();
                            }
                            catch { }
                        });

                        PlaywrightBase.GotoAsync(page, uri);

                        for (int i = 0; i < 10*5; i++) // 5 second
                        {
                            if (browser.IsCompleted)
                                break;

                            foreach (string playBtnSelector in new string[] { "div.flex.flex-col.items-center.gap-y-3.title-year > button" })
                            {
                                try
                                {
                                    var playBtn = await page.QuerySelectorAsync(playBtnSelector);
                                    if (playBtn != null)
                                        await playBtn.ClickAsync();
                                }
                                catch { }
                            }

                            await Task.Delay(100);
                        }

                        // await browser.WaitPageResult()
                        cache.m3u8 = await browser.completionSource.Task;
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

using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Shared.PlaywrightCore;
using Shared.Models.Online.Settings;

namespace Online.Controllers
{
    public class MovPI : BaseENGController
    {
        [HttpGet]
        [Route("lite/movpi")]
        public ValueTask<ActionResult> Index(bool checksearch, long id, long tmdb_id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(AppInit.conf.MovPI, checksearch, id, tmdb_id, imdb_id, title, original_title, serial, s, rjson, method: "call");
        }


        #region Video
        [HttpGet]
        [Route("lite/movpi/video")]
        [Route("lite/movpi/video.m3u8")]
        async public ValueTask<ActionResult> Video(long id, int s = -1, int e = -1, bool play = false)
        {
            var init = await loadKit(AppInit.conf.MovPI);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (id == 0)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/movie/{id}?autoPlay=true&poster=false";
            if (s > 0)
                embed = $"{init.host}/tv/{id}-{s}-{e}?autoPlay=true&poster=false";

            return await InvkSemaphore(init, embed, async () =>
            {
                var cache = await black_magic(embed, init, proxyManager, proxy.data);
                if (cache.m3u8 == null)
                    return StatusCode(502);

                var headers_stream = httpHeaders(init.host, init.headers_stream);
                if (headers_stream.Count == 0)
                    headers_stream = cache.headers;

                string hls = HostStreamProxy(init, cache.m3u8, proxy: proxy.proxy, headers: headers_stream);

                if (play)
                    return RedirectToPlay(hls);

                return ContentTo(VideoTpl.ToJson("play", hls, "English", vast: init.vast, headers: init.streamproxy ? null : headers_stream));
            });
        }
        #endregion

        #region black_magic
        async ValueTask<(string m3u8, List<HeadersModel> headers)> black_magic(string uri, OnlinesSettings init, ProxyManager proxyManager, (string ip, string username, string password) proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return default;

            try
            {
                string memKey = $"movpi:black_magic:{uri}:{proxy.ip}";
                if (!hybridCache.TryGetValue(memKey, out (string m3u8, List<HeadersModel> headers) cache))
                {
                    if (init.priorityBrowser == "scraping")
                    {
                        #region Scraping
                        using (var browser = new Scraping(uri, "\\.m3u8", null))
                        {
                            browser.OnRequest += e =>
                            {
                                if (uri == e.HttpClient.Request.Url)
                                    e.HttpClient.Request.Headers.AddHeader("Referer", CrypTo.DecodeBase64("aHR0cHM6Ly93d3cuaHlkcmFmbGl4LnZpcC8="));
                            };

                            var scrap = await browser.WaitPageResult(20);

                            if (scrap != null)
                            {
                                cache.m3u8 = scrap.Url;
                                cache.headers = new List<HeadersModel>();

                                foreach (var item in scrap.Headers)
                                {
                                    if (item.Name.ToLower() is "host" or "accept-encoding" or "connection" or "range")
                                        continue;

                                    cache.headers.Add(new HeadersModel(item.Name, item.Value));
                                }
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        #region Playwright
                        using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                        {
                            var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy);
                            if (page == null)
                                return default;

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
                                        PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                        await route.AbortAsync();
                                        return;
                                    }

                                    if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                        return;

                                    if (route.Request.Url == uri)
                                    {
                                        await route.ContinueAsync(new RouteContinueOptions
                                        {
                                            Headers = httpHeaders(init, HeadersModel.Init(("referer", CrypTo.DecodeBase64("aHR0cHM6Ly93d3cuaHlkcmFmbGl4LnZpcC8=")))).ToDictionary()
                                        });
                                        return;
                                    }

                                    if (route.Request.Url.Contains(".m3u8"))
                                    {
                                        cache.headers = new List<HeadersModel>();
                                        foreach (var item in route.Request.Headers)
                                        {
                                            if (item.Key.ToLower() is "host" or "accept-encoding" or "connection" or "range")
                                                continue;

                                            cache.headers.Add(new HeadersModel(item.Key, item.Value.ToString()));
                                        }

                                        PlaywrightBase.ConsoleLog($"Playwright: SET {route.Request.Url}", cache.headers);
                                        browser.SetPageResult(route.Request.Url);
                                        await route.AbortAsync();
                                        return;
                                    }

                                    await route.ContinueAsync();
                                }
                                catch { }
                            });

                            PlaywrightBase.GotoAsync(page, PlaywrightBase.IframeUrl(uri));

                            cache.m3u8 = await browser.WaitPageResult(20);
                        }
                        #endregion
                    }

                    if (cache.m3u8 == null)
                    {
                        proxyManager.Refresh();
                        return default;
                    }

                    proxyManager.Success();
                    hybridCache.Set(memKey, cache, cacheTime(20, init: init));
                }

                return cache;
            }
            catch { return default; }
        }
        #endregion
    }
}

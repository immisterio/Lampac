using Microsoft.AspNetCore.Mvc;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class HydraFlix : BaseENGController
    {
        public HydraFlix() : base(AppInit.conf.Hydraflix) { }

        [HttpGet]
        [Route("lite/hydraflix")]
        public Task<ActionResult> Index(bool checksearch, long id, long tmdb_id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(checksearch, id, tmdb_id, imdb_id, title, original_title, serial, s, rjson, method: "call", extension: "m3u8");
        }

        #region Video
        [HttpGet]
        [Route("lite/hydraflix/video")]
        [Route("lite/hydraflix/video.mpd")]
        [Route("lite/hydraflix/video.m3u8")]
        async public ValueTask<ActionResult> Video(long id, int s = -1, int e = -1, bool play = false)
        {
            if (id == 0)
                return OnError();

            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                return OnError();

            if (await IsRequestBlocked(rch: false, rch_check: !play))
                return badInitMsg;

            string embed = $"{init.host}/movie/{id}?autoPlay=true&theme=e1216d";
            if (s > 0)
                embed = $"{init.host}/tv/{id}/{s}/{e}?autoPlay=true&theme=e1216d";

            return await InvkSemaphore(embed, async () =>
            {
                var cache = await black_magic(embed);
                if (cache.m3u8 == null)
                    return StatusCode(502);

                var headers_stream = httpHeaders(init.host, init.headers_stream);
                if (headers_stream.Count == 0)
                    headers_stream = cache.headers;

                string file = HostStreamProxy(cache.m3u8, headers: headers_stream);

                if (play)
                    return RedirectToPlay(file);

                return ContentTo(VideoTpl.ToJson("play", file, "English", vast: init.vast, headers: init.streamproxy ? null : headers_stream));
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
                string memKey = $"Hydraflix:black_magic:{uri}";
                if (!hybridCache.TryGetValue(memKey, out (string m3u8, List<HeadersModel> headers) cache))
                {
                    if (init.priorityBrowser == "scraping")
                    {
                        #region Scraping
                        using (var browser = new Scraping(uri, "\\.(mpd|m3u|mp4)", null))
                        {
                            //browser.OnRequest += e =>
                            //{
                            //    if (Regex.IsMatch(e.HttpClient.Request.Url, "\\.(css|woff2|jpe?g|png|ico)"))
                            //        e.Ok(string.Empty);
                            //};

                            var scrap = await browser.WaitPageResult(20);

                            if (scrap != null)
                            {
                                cache.m3u8 = scrap.Url;
                                cache.headers = new List<HeadersModel>();

                                foreach (var item in scrap.Headers)
                                {
                                    if (item.Name.ToLower() is "host" or "accept-encoding" or "connection" or "range" or "cookie")
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
                            var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy_data);
                            if (page == null)
                                return default;

                            await page.RouteAsync("**/*", async route =>
                            {
                                try
                                {
                                    if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                        return;

                                    if (browser.IsCompleted || route.Request.Url.Contains("adsco."))
                                    {
                                        PlaywrightBase.ConsoleLog(() => $"Playwright: Abort {route.Request.Url}");
                                        await route.AbortAsync();
                                        return;
                                    }

                                    if (Regex.IsMatch(route.Request.Url, "\\.(mpd|m3u|mp4)"))
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
                                        await route.AbortAsync();
                                        return;
                                    }

                                    await route.ContinueAsync();
                                }
                                catch { }
                            });

                            PlaywrightBase.GotoAsync(page, uri);
                            cache.m3u8 = await browser.WaitPageResult(20);
                        }
                        #endregion
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
            catch 
            { 
                return default; 
            }
        }
        #endregion
    }
}

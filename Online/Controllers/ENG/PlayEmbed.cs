using Microsoft.AspNetCore.Mvc;
using Shared.Models.Online.Settings;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class PlayEmbed : BaseENGController
    {
        [HttpGet]
        [Route("lite/playembed")]
        public ValueTask<ActionResult> Index(bool checksearch, long id, long tmdb_id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(AppInit.conf.Playembed, checksearch, id, tmdb_id,imdb_id, title, original_title, serial, s, rjson, method: "call");
        }


        #region Video
        [HttpGet]
        [Route("lite/playembed/video")]
        [Route("lite/playembed/video.m3u8")]
        async public ValueTask<ActionResult> Video(long id, int s = -1, int e = -1, bool play = false)
        {
            var init = await loadKit(AppInit.conf.Playembed);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/movie/{id}?colour=e1216d&autoplay=true&autonextepisode=false&pausescreen=true";
            if (s > 0)
                embed = $"{init.host}/tv/{id}/{s}/{e}?colour=e1216d&autoplay=true&autonextepisode=false&pausescreen=true";

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
                string memKey = $"playembed:black_magic:{uri}";
                if (!hybridCache.TryGetValue(memKey, out (string m3u8, List<HeadersModel> headers) cache))
                {
                    using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                    {
                        var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy);
                        if (page == null)
                            return default;

                        await page.RouteAsync("**/*", async route =>
                        {
                            try
                            {
                                if (browser.IsCompleted || Regex.IsMatch(route.Request.Url, "(/ads/)"))
                                {
                                    PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                    return;
                                }

                                if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                    return;

                                if (route.Request.Url.Contains(".m3u8") || route.Request.Url.Contains("/playlist/"))
                                {
                                    cache.headers = HeadersModel.Init(
                                        ("sec-fetch-dest", "empty"),
                                        ("sec-fetch-mode", "cors"),
                                        ("sec-fetch-site", "cross-site")
                                    );

                                    foreach (var item in route.Request.Headers)
                                    {
                                        if (item.Key.ToLower() is "host" or "accept-encoding" or "connection" or "range")
                                            continue;

                                        if (cache.headers.FirstOrDefault(k => k.name == item.Key) == null)
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

                        PlaywrightBase.GotoAsync(page, uri);
                        cache.m3u8 = await browser.WaitPageResult(20);
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

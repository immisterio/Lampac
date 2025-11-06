using Microsoft.AspNetCore.Mvc;
using Shared.Models.Online.Settings;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class AutoEmbed : BaseENGController
    {
        [HttpGet]
        [Route("lite/autoembed")]
        public ValueTask<ActionResult> Index(bool checksearch, long id, long tmdb_id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            var init = AppInit.conf.Autoembed;
            return ViewTmdb(init, checksearch, id, tmdb_id, imdb_id, title, original_title, serial, s, rjson, mp4: true, method: "call");
        }


        #region Video
        [HttpGet]
        [Route("lite/autoembed/video")]
        async public ValueTask<ActionResult> Video(long id, int s = -1, int e = -1, bool play = false)
        {
            var init = await loadKit(AppInit.conf.Autoembed);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/embed/movie/{id}";
            if (s > 0)
                embed = $"{init.host}/embed/tv/{id}/{s}/{e}";

            return await InvkSemaphore(init, embed, async () =>
            {
                var cache = await black_magic(embed, init, proxyManager, proxy.data);
                if (cache.file == null)
                    return StatusCode(502);

                var headers_stream = httpHeaders(init.host, init.headers_stream);
                if (headers_stream.Count == 0)
                    headers_stream = cache.headers;

                string file = HostStreamProxy(init, cache.file, proxy: proxy.proxy, headers: headers_stream);

                if (play)
                    return RedirectToPlay(file);

                return ContentTo(VideoTpl.ToJson("play", file, "English", vast: init.vast, headers: init.streamproxy ? null : headers_stream));
            });
        }
        #endregion

        #region black_magic
        async ValueTask<(string file, List<HeadersModel> headers)> black_magic(string uri, OnlinesSettings init, ProxyManager proxyManager, (string ip, string username, string password) proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return default;

            try
            {
                string memKey = $"autoembed:black_magic:{uri}";
                if (!hybridCache.TryGetValue(memKey, out (string file, List<HeadersModel> headers) cache))
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
                                if (cache.file != null || await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                    return;

                                if ((Regex.IsMatch(route.Request.Url, "(hakunaymatata|kphimplayer)") && route.Request.Url.Contains(".mp4")) 
                                    || route.Request.Url.Contains("/embed-proxy?url=")
                                    || route.Request.Url.Contains(".m3u8"))
                                {
                                    cache.headers = HeadersModel.Init(
                                        ("sec-fetch-dest", "empty"),
                                        ("sec-fetch-mode", "cors"),
                                        ("sec-fetch-site", "cross-site"), 
                                        ("referer", $"{init.host}/"),
                                        ("origin", init.host)
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

                                if (browser.IsCompleted || Regex.IsMatch(route.Request.Url, "(/ads/|vast.xml|ping.gif|silent.mp4)"))
                                {
                                    PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                    return;
                                }

                                await route.ContinueAsync();
                            }
                            catch { }
                        });

                        PlaywrightBase.GotoAsync(page, uri);
                        cache.file = await browser.WaitPageResult(20);
                    }

                    if (cache.file == null)
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

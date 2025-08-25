using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Shared.Models.Online.Settings;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class SmashyStream : BaseENGController
    {
        [HttpGet]
        [Route("lite/smashystream")]
        public ValueTask<ActionResult> Index(bool checksearch, long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(AppInit.conf.Smashystream, checksearch, id, imdb_id, title, original_title, serial, s, rjson, hls_manifest_timeout: (int)TimeSpan.FromSeconds(35).TotalMilliseconds);
        }


        #region Video
        [HttpGet]
        [Route("lite/smashystream/video.m3u8")]
        async public ValueTask<ActionResult> Video(long id, int s = -1, int e = -1)
        {
            var init = await loadKit(AppInit.conf.Smashystream);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (id == 0)
                return OnError();

            if (Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/movie/{id}";
            if (s > 0)
                embed = $"{init.host}/tv/{id}?s={s}&e={e}";

            var cache = await black_magic(embed, init, proxyManager, proxy.data);
            if (cache.m3u8 == null)
                return StatusCode(502);

            return Redirect(HostStreamProxy(init, cache.m3u8, proxy: proxy.proxy, headers: cache.headers));
        }
        #endregion

        #region black_magic
        async ValueTask<(string m3u8, List<HeadersModel> headers)> black_magic(string uri, OnlinesSettings init, ProxyManager proxyManager, (string ip, string username, string password) proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return default;

            try
            {
                string memKey = $"smashystream:black_magic:{uri}";
                if (!hybridCache.TryGetValue(memKey, out (string m3u8, List<HeadersModel> headers) cache))
                {
                    using (var browser = new Firefox())
                    {
                        var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy);
                        if (page == null)
                            return default;

                        await page.RouteAsync("**/*", async route =>
                        {
                            try
                            {
                                if (browser.IsCompleted || route.Request.Url.Contains(".m3u") || Regex.IsMatch(route.Request.Url, "(\\.vtt|histats.com|solid.gif|poster.png|inkblotconusor\\.|jrbbavbvqmrjw\\.)"))
                                {
                                    PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                    return;
                                }

                                if (await PlaywrightBase.AbortOrCache(page, route, fullCacheJS: true))
                                    return;

                                if (route.Request.Url.Contains("master.txt"))
                                {
                                    cache.headers = new List<HeadersModel>();
                                    foreach (var item in route.Request.Headers)
                                    {
                                        if (item.Key.ToLower() is "host" or "accept-encoding" or "connection" or "range")
                                            continue;

                                        cache.headers.Add(new HeadersModel(item.Key, item.Value.ToString()));
                                    }

                                    PlaywrightBase.ConsoleLog($"Playwright: SET {route.Request.Url}", cache.headers);
                                    browser.IsCompleted = true;
                                    cache.m3u8 = route.Request.Url;
                                    await route.AbortAsync();
                                    return;
                                }

                                await route.ContinueAsync();
                            }
                            catch { }
                        });

                        PlaywrightBase.GotoAsync(page, uri);
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                        var viewportSize = await page.EvaluateAsync<ViewportSize>("() => ({ width: window.innerWidth, height: window.innerHeight })");

                        var endTime = DateTime.Now.AddSeconds(20);
                        while (endTime > DateTime.Now && cache.m3u8 == null)
                        {
                            int vS(int center)
                            {
                                var centerX = center / 2;
                                return Random.Shared.Next(0, 3) == 1 ? (centerX + Random.Shared.Next(1, 20)) : (centerX - Random.Shared.Next(1, 20));
                            }

                            await Task.Delay(100);
                            await page.Mouse.ClickAsync(vS(viewportSize.Width), vS(viewportSize.Height));
                        }
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

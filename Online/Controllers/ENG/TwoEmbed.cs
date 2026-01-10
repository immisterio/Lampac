using Microsoft.AspNetCore.Mvc;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class TwoEmbed : BaseENGController
    {
        public TwoEmbed() : base(AppInit.conf.Twoembed) { }

        [HttpGet]
        [Route("lite/twoembed")]
        public Task<ActionResult> Index(bool checksearch, long id, long tmdb_id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(checksearch, id, tmdb_id, imdb_id, title, original_title, serial, s, rjson, method: "call");
        }

        #region Video
        [HttpGet]
        [Route("lite/twoembed/video")]
        [Route("lite/twoembed/video.m3u8")]
        async public ValueTask<ActionResult> Video(long id, int s = -1, int e = -1, bool play = false)
        {
            if (await IsRequestBlocked(rch: false, rch_check: !play))
                return badInitMsg;

            if (Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

            string embed = $"{init.host}/embed/movie/{id}";
            if (s > 0)
                embed = $"{init.host}/embed/tv/{id}/{s}/{e}";

            return await InvkSemaphore(embed, async () =>
            {
                var hls = await black_magic(embed);
                if (hls == null)
                    return StatusCode(502);

                hls = HostStreamProxy(hls);

                if (play)
                    return RedirectToPlay(hls);

                return ContentTo(VideoTpl.ToJson("play", hls, "English", vast: init.vast, headers: init.streamproxy ? null : httpHeaders(init.host, init.headers_stream)));
            });
        }
        #endregion

        #region black_magic
        async ValueTask<string> black_magic(string uri)
        {
            if (string.IsNullOrEmpty(uri))
                return default;

            try
            {
                string memKey = $"twoembed:black_magic:{uri}";
                if (!hybridCache.TryGetValue(memKey, out string m3u8))
                {
                    using (var browser = new Firefox())
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

                                if (browser.IsCompleted || Regex.IsMatch(route.Request.Url, "(fonts.googleapis|pixel.embed|rtmark)\\."))
                                {
                                    PlaywrightBase.ConsoleLog(() => $"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                    return;
                                }

                                if (route.Request.Url.Contains(".m3u8"))
                                {
                                    PlaywrightBase.ConsoleLog(() => ($"Playwright: SET {route.Request.Url}"));
                                    browser.IsCompleted = true;
                                    browser.completionSource.SetResult(route.Request.Url);
                                    await route.AbortAsync();
                                    return;
                                }

                                await route.ContinueAsync();
                            }
                            catch { }
                        });

                        PlaywrightBase.GotoAsync(page, uri);
                        m3u8 = await browser.WaitPageResult(20);
                    }

                    if (m3u8 == null)
                    {
                        proxyManager?.Refresh();
                        return default;
                    }

                    proxyManager?.Success();
                    hybridCache.Set(memKey, m3u8, cacheTime(20));
                }

                return m3u8;
            }
            catch { return default; }
        }
        #endregion
    }
}

using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.PlaywrightCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Videasy;

public class VideasyController : BaseENGController
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<VideasyController>();

    public VideasyController() : base(ModInit.conf) { }

    [HttpGet]
    [Route("lite/videasy")]
    public Task<ActionResult> Index(bool checksearch, long id, long tmdb_id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
    {
        return ViewTmdb(checksearch, id, tmdb_id, imdb_id, title, original_title, serial, s, rjson, method: "call");
    }

    [HttpGet]
    [Route("lite/videasy/video")]
    [Route("lite/videasy/video.m3u8")]
    public async Task<ActionResult> Video(long id, int s = -1, int e = -1, bool play = false)
    {
        if (await IsRequestBlocked(rch: false, rch_check: !play))
            return badInitMsg;

        if (id == 0)
            return OnError();

        string embed = $"{init.host}/movie/{id}";
        if (s > 0)
            embed = $"{init.host}/tv/{id}/{s}/{e}";

        var cache = await InvokeCacheResult<(string hls, List<HeadersModel> headers)>(ipkey($"videasy:video:{embed}"), 20, async e =>
        {
            var blackMagic = await black_magic(embed);
            if (blackMagic.m3u8 == null)
                return e.Fail("m3u8");

            var headers_stream = httpHeaders(init.host, init.headers_stream);
            if (headers_stream == null || headers_stream.Count == 0)
                headers_stream = blackMagic.headers;

            string hls = HostStreamProxy(blackMagic.m3u8, headers: headers_stream);
            if (string.IsNullOrEmpty(hls))
                return e.Fail("hls");

            return e.Success((hls, init.streamproxy ? null : headers_stream));
        });

        if (!cache.IsSuccess)
            return StatusCode(502);

        if (play)
            return RedirectToPlay(cache.Value.hls);

        return ContentTo(VideoTpl.ToJson(
            "play",
            cache.Value.hls,
            "English",
            vast: init.vast,
            headers: cache.Value.headers,
            httpContext: HttpContext
        ));
    }

    private async Task<(string m3u8, List<HeadersModel> headers)> black_magic(string uri)
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
                        catch (System.Exception ex)
                        {
                            Log.Error(ex, "CatchId={CatchId}", "id_dmgyxur2");
                        }
                    });

                    PlaywrightBase.GotoAsync(page, uri);

                    for (int i = 0; i < 10 * 5; i++)
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
                            catch (System.Exception ex)
                            {
                                Log.Error(ex, "CatchId={CatchId}", "id_2wdspkzi");
                            }
                        }

                        await Task.Delay(100);
                    }

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
        catch
        {
            return default;
        }
    }
}

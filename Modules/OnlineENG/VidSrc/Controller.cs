using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Shared;
using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.PlaywrightCore;
using Shared.Services;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VidSrc;

public class VidSrcController : BaseENGController
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<VidSrcController>();
    private static List<HeadersModel> lastHeaders;

    public VidSrcController() : base(ModInit.conf)
    {
    }

    [HttpGet]
    [Route("lite/vidsrc")]
    public Task<ActionResult> Index(bool checksearch, long id, long tmdb_id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
    {
        return ViewTmdb(checksearch, id, tmdb_id, imdb_id, title, original_title, serial, s, rjson, method: "call");
    }

    [HttpGet]
    [Route("lite/vidsrc/video")]
    [Route("lite/vidsrc/video.m3u8")]
    public async Task<ActionResult> Video(long id, string imdb_id, int s = -1, int e = -1, bool play = false)
    {
        if (id == 0)
            return OnError();

        if (await IsRequestBlocked(rch: false, rch_check: !play))
            return badInitMsg;

        string embed = $"{init.host}/v2/embed/movie/{id}?autoPlay=true&poster=false";
        if (s > 0)
            embed = $"{init.host}/v2/embed/tv/{id}/{s}/{e}?autoPlay=true&poster=false";

        var cache = await InvokeCacheResult<(string file, List<HeadersModel> headers, SubtitleTpl subtitles)>(embed, 20, async result =>
        {
            if (memoryCache.TryGetValue($"vidsrc:lastvrf:{id}", out string vrf) && s > 0)
            {
                string uri = $"{init.host}/api/{id}/servers?id={id}&type=tv&season={s}&episode={e}&vrf={vrf}&imdbId={imdb_id}";
                if (!hybridCache.TryGetValue(uri, out JToken data))
                {
                    try
                    {
                        var root = await Http.Get<JObject>(uri, timeoutSeconds: 8);
                        if (root != null && root.ContainsKey("data"))
                        {
                            string hash = root["data"].First.Value<string>("hash");
                            var source = await Http.Get<JObject>($"{init.host}/api/source/{hash}", timeoutSeconds: 8);
                            if (source != null && source.ContainsKey("data"))
                            {
                                data = source["data"];
                                hybridCache.Set(uri, data, cacheTime(20));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "CatchId={CatchId}", "id_owu3uxfl");
                    }
                }

                if (data != null)
                {
                    var subtitles = new SubtitleTpl();
                    try
                    {
                        foreach (var sub in data["subtitles"])
                            subtitles.Append(sub.Value<string>("label"), HostStreamProxy(sub.Value<string>("file")));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "CatchId={CatchId}", "id_ejc3a36i");
                    }

                    var streamHeaders = httpHeaders(init.host, init.headers_stream);
                    if (streamHeaders == null || streamHeaders.Count == 0)
                        streamHeaders = lastHeaders;

                    string file = HostStreamProxy(data.Value<string>("source"), headers: streamHeaders);
                    return result.Success((file, streamHeaders, subtitles));
                }
            }

            var extracted = await ExtractPlaylist(id, embed);
            if (extracted.m3u8 == null)
                return result.Fail("m3u8");

            var headers = httpHeaders(init.host, init.headers_stream);
            if (headers == null || headers.Count == 0)
                headers = extracted.headers;

            string hls = HostStreamProxy(extracted.m3u8, headers: headers);
            return result.Success((hls, headers, null));
        });

        if (!cache.IsSuccess || string.IsNullOrEmpty(cache.Value.file))
            return StatusCode(502);

        if (play)
            return RedirectToPlay(cache.Value.file);

        return ContentTo(VideoTpl.ToJson(
            "play",
            cache.Value.file,
            "English",
            subtitles: cache.Value.subtitles,
            vast: init.vast,
            headers: init.streamproxy ? null : cache.Value.headers,
            httpContext: HttpContext
        ));
    }

    private async Task<(string m3u8, List<HeadersModel> headers)> ExtractPlaylist(long id, string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return default;

        try
        {
            string memKey = $"vidsrc:black_magic:{uri}";
            if (!hybridCache.TryGetValue(memKey, out (string m3u8, List<HeadersModel> headers) cache))
            {
                using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                {
                    var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy_data);
                    if (page == null)
                        return default;

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            if (browser.IsCompleted || Regex.IsMatch(route.Request.Url.Split("?")[0], "\\.(woff2?|vtt|srt|css|ico)$"))
                            {
                                PlaywrightBase.ConsoleLog(() => $"Playwright: Abort {route.Request.Url}");
                                await route.AbortAsync();
                                return;
                            }

                            if (await PlaywrightBase.AbortOrCache(page, route, fullCacheJS: true))
                                return;

                            if (Regex.IsMatch(route.Request.Url, "/api/[0-9]+/servers"))
                            {
                                string vrf = Regex.Match(route.Request.Url, "&vrf=([^&]+)").Groups[1].Value;
                                if (!string.IsNullOrEmpty(vrf) && route.Request.Url.Contains("&type=tv"))
                                    memoryCache.Set($"vidsrc:lastvrf:{id}", vrf, DateTime.Now.AddDays(1));
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

                                lastHeaders = cache.headers;
                                PlaywrightBase.ConsoleLog(() => ($"Playwright: SET {route.Request.Url}", cache.headers));
                                browser.SetPageResult(route.Request.Url);
                                await route.AbortAsync();
                                return;
                            }

                            await route.ContinueAsync();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "CatchId={CatchId}", "id_zp5in04r");
                        }
                    });

                    PlaywrightBase.GotoAsync(page, uri);

                    for (int i = 0; i < 150; i++)
                    {
                        if (browser.IsCompleted)
                            break;

                        try
                        {
                            var playBtn = await page.QuerySelectorAsync("#btn-play");
                            if (playBtn != null)
                                await playBtn.ClickAsync();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "CatchId={CatchId}", "id_o2bkg9te");
                        }

                        await Task.Delay(100);
                    }

                    cache.m3u8 = await browser.completionSource.Task;
                }

                if (cache.m3u8 == null)
                {
                    proxyManager.Refresh();
                    return default;
                }

                proxyManager.Success();
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

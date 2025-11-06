using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Settings;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class VidSrc : BaseENGController
    {
        [HttpGet]
        [Route("lite/vidsrc")]
        public ValueTask<ActionResult> Index(bool checksearch, long id, long tmdb_id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(AppInit.conf.Vidsrc, checksearch, id, tmdb_id, imdb_id, title, original_title, serial, s, rjson, method: "call");
        }


        #region Video
        static List<HeadersModel> lastHeaders = null;


        [HttpGet]
        [Route("lite/vidsrc/video")]
        [Route("lite/vidsrc/video.m3u8")]
        async public ValueTask<ActionResult> Video(long id, string imdb_id, int s = -1, int e = -1, bool play = false)
        {
            var init = await loadKit(AppInit.conf.Vidsrc);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (id == 0)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/v2/embed/movie/{id}?autoPlay=true&poster=false";
            if (s > 0)
                embed = $"{init.host}/v2/embed/tv/{id}/{s}/{e}?autoPlay=true&poster=false";

            return await InvkSemaphore(init, embed, async () =>
            {
                #region api servers
                if (memoryCache.TryGetValue($"vidsrc:lastvrf:{id}", out string _vrf) && s > 0)
                {
                    string uri = $"{init.host}/api/{id}/servers?id={id}&type=tv&season={s}&episode={e}&vrf={_vrf}&imdbId={imdb_id}";
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
                        catch { }
                    }

                    if (data != null)
                    {
                        var subtitles = new SubtitleTpl();
                        try
                        {
                            foreach (var sub in data["subtitles"])
                                subtitles.Append(sub.Value<string>("label"), HostStreamProxy(init, sub.Value<string>("file"), proxy: proxy.proxy));
                        }
                        catch { }

                        var lastHeaders_headers = httpHeaders(init.host, init.headers_stream);
                        if (lastHeaders_headers.Count == 0)
                            lastHeaders_headers = lastHeaders;

                        string file = HostStreamProxy(init, data.Value<string>("source"), proxy: proxy.proxy, headers: lastHeaders_headers);
                        if (play)
                            return RedirectToPlay(file);

                        return ContentTo(VideoTpl.ToJson("play", file, "English", subtitles: subtitles, vast: init.vast, headers: init.streamproxy ? null : lastHeaders_headers));
                    }
                }
                #endregion

                var cache = await black_magic(id, embed, init, proxyManager, proxy.data);
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
        async ValueTask<(string m3u8, List<HeadersModel> headers)> black_magic(long id, string uri, OnlinesSettings init, ProxyManager proxyManager, (string ip, string username, string password) proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return default;

            try
            {
                string memKey = $"vidsrc:black_magic:{uri}";
                if (!hybridCache.TryGetValue(memKey, out (string m3u8, List<HeadersModel> headers) cache))
                {
                    if (init.priorityBrowser == "scraping")
                    {
                        #region Scraping
                        using (var browser = new Scraping(uri, "\\.m3u8", null))
                        {
                            browser.OnRequest += e =>
                            {
                                if (Regex.IsMatch(e.HttpClient.Request.Url, "/api/[0-9]+/servers"))
                                {
                                    string vrf = Regex.Match(e.HttpClient.Request.Url, "&vrf=([^&]+)").Groups[1].Value;
                                    if (!string.IsNullOrEmpty(vrf) && e.HttpClient.Request.Url.Contains("&type=tv"))
                                        memoryCache.Set($"vidsrc:lastvrf:{id}", vrf, DateTime.Now.AddDays(1));
                                }
                                else if (Regex.IsMatch(e.HttpClient.Request.Url.Split("?")[0], "\\.(woff2?|vtt|srt|css|ico)$"))
                                    e.Ok(string.Empty);
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
                                    if (browser.IsCompleted || Regex.IsMatch(route.Request.Url.Split("?")[0], "\\.(woff2?|vtt|srt|css|ico)$"))
                                    {
                                        PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
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

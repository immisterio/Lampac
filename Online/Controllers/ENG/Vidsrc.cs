using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Engine;
using Lampac.Models.LITE;
using System;
using System.Text.RegularExpressions;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using Shared.Model.Templates;
using System.Collections.Concurrent;
using Shared.Model.Online;
using System.Collections.Generic;

namespace Lampac.Controllers.LITE
{
    public class VidSrc : BaseENGController
    {
        [HttpGet]
        [Route("lite/vidsrc")]
        public Task<ActionResult> Index(bool checksearch, long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(AppInit.conf.Vidsrc, true, checksearch, id, imdb_id, title, original_title, serial, s, rjson, method: "call");
        }


        #region Video
        static ConcurrentDictionary<long, string> lastvrf = new ConcurrentDictionary<long, string>();

        static List<HeadersModel> lastHeaders = null;


        [HttpGet]
        [Route("lite/vidsrc/video")]
        [Route("lite/vidsrc/video.m3u8")]
        async public Task<ActionResult> Video(long id, string imdb_id, int s = -1, int e = -1, bool play = false)
        {
            var init = await loadKit(AppInit.conf.Vidsrc);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (id == 0)
                return OnError();

            if (Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/v2/embed/movie/{id}?autoPlay=true&poster=false";
            if (s > 0)
                embed = $"{init.host}/v2/embed/tv/{id}/{s}/{e}?autoPlay=true&poster=false";

            #region api servers
            if (lastvrf.ContainsKey(id) && s > 0)
            {
                string uri = $"{init.host}/api/{id}/servers?id={id}&type=tv&season={s}&episode={e}&vrf={lastvrf[id]}&imdbId={imdb_id}";
                if (!hybridCache.TryGetValue(uri, out JToken data))
                {
                    try
                    {
                        var root = await HttpClient.Get<JObject>(uri, timeoutSeconds: 8);
                        if (root != null && root.ContainsKey("data"))
                        {
                            string hash = root["data"].First.Value<string>("hash");
                            var source = await HttpClient.Get<JObject>($"{init.host}/api/source/{hash}", timeoutSeconds: 8);
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

                    string file = HostStreamProxy(init, data.Value<string>("source"), proxy: proxy.proxy, headers: lastHeaders);
                    if (play)
                        return Redirect(file);

                    return ContentTo(VideoTpl.ToJson("play", file, "English", subtitles: subtitles, vast: init.vast));
                }
            }
            #endregion

            var cache = await black_magic(id, embed, init, proxy.data);
            if (cache.m3u8 == null)
                return StatusCode(502);

            string hls = HostStreamProxy(init, cache.m3u8, proxy: proxy.proxy, headers: cache.headers);

            if (play)
                return Redirect(hls);

            return ContentTo(VideoTpl.ToJson("play", hls, "English", vast: init.vast, headers: cache.headers));
        }
        #endregion

        #region black_magic
        async ValueTask<(string m3u8, List<HeadersModel> headers)> black_magic(long id, string uri, OnlinesSettings init, (string ip, string username, string password) proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return default;

            try
            {
                string memKey = $"vidsrc:black_magic:{uri}";
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
                                if (browser.IsCompleted || Regex.IsMatch(route.Request.Url.Split("?")[0], "\\.(woff2?|vtt|srt|css|ico)$"))
                                {
                                    Console.WriteLine($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                    return;
                                }

                                if (await PlaywrightBase.AbortOrCache(page, route, fullCacheJS: true))
                                    return;

                                if (Regex.IsMatch(route.Request.Url, "/api/[0-9]+/servers"))
                                {
                                    string vrf = Regex.Match(route.Request.Url, "&vrf=([^&]+)").Groups[1].Value;
                                    if (!string.IsNullOrEmpty(vrf) && route.Request.Url.Contains("&type=tv"))
                                        lastvrf.AddOrUpdate(id, vrf, (k, v) => vrf);
                                }

                                if (route.Request.Url.Contains(".m3u8"))
                                {
                                    cache.headers = new List<HeadersModel>();
                                    foreach (var item in route.Request.Headers)
                                    {
                                        if (item.Key.ToLower() is "host" or "accept-encoding" or "connection")
                                            continue;

                                        cache.headers.Add(new HeadersModel(item.Key, item.Value.ToString()));
                                    }

                                    lastHeaders = cache.headers;

                                    Console.WriteLine($"Playwright: SET {route.Request.Url}");
                                    browser.IsCompleted = true;
                                    browser.completionSource.SetResult(route.Request.Url);
                                    await route.AbortAsync();
                                    return;
                                }

                                await route.ContinueAsync();
                            }
                            catch { }
                        });

                        var response = await page.GotoAsync(uri);
                        if (response == null)
                            return default;

                        cache.m3u8 = await browser.WaitPageResult();
                    }

                    if (cache.m3u8 == null)
                        return default;

                    hybridCache.Set(memKey, cache, cacheTime(20, init: init));
                }

                return cache;
            }
            catch { return default; }
        }
        #endregion
    }
}

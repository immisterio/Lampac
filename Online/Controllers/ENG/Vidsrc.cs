using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Engine;
using Lampac.Models.LITE;
using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using Shared.Model.Templates;
using System.Collections.Concurrent;

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

                    string file = HostStreamProxy(init, data.Value<string>("source"), proxy: proxy.proxy);
                    if (play)
                        return Redirect(file);

                    return ContentTo(VideoTpl.ToJson("play", file, "English", subtitles: subtitles, vast: init.vast));
                }
            }
            #endregion

            string hls = await black_magic(id, embed, init, proxy.data);
            if (hls == null)
                return StatusCode(502);

            hls = HostStreamProxy(init, hls, proxy: proxy.proxy);

            if (play)
                return Redirect(hls);

            return ContentTo(VideoTpl.ToJson("play", hls, "English", vast: init.vast));
        }
        #endregion

        #region black_magic
        async ValueTask<string> black_magic(long id, string uri, OnlinesSettings init, (string ip, string username, string password) proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return uri;

            try
            {
                string memKey = $"vidsrc:black_magic:{uri}";
                if (!memoryCache.TryGetValue(memKey, out string m3u8))
                {
                    using (var browser = new Firefox())
                    {
                        var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy);
                        if (page == null)
                            return null;

                        await page.RouteAsync("**/*", async route =>
                        {
                            if (await PlaywrightBase.AbortOrCache(memoryCache, page, route, abortMedia: true, fullCacheJS: true))
                                return;

                            if (Regex.IsMatch(route.Request.Url, "/api/[0-9]+/servers"))
                            {
                                string vrf = Regex.Match(route.Request.Url, "&vrf=([^&]+)").Groups[1].Value;
                                if (!string.IsNullOrEmpty(vrf) && route.Request.Url.Contains("&type=tv"))
                                    lastvrf.AddOrUpdate(id, vrf, (k, v) => vrf);
                            }

                            if (route.Request.Url.Contains(".m3u8"))
                            {
                                browser.completionSource.SetResult(route.Request.Url);
                                await route.AbortAsync();
                                return;
                            }

                            await route.ContinueAsync();
                        });

                        var response = await page.GotoAsync(uri);
                        if (response == null)
                            return null;

                        m3u8 = await browser.WaitPageResult();
                    }

                    if (m3u8 == null)
                        return null;

                    memoryCache.Set(memKey, m3u8, cacheTime(20, init: init));
                }

                return m3u8;
            }
            catch { return null; }
        }
        #endregion
    }
}

﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Engine;
using Lampac.Models.LITE;
using Shared.PlaywrightCore;
using Shared.Model.Online;
using System.Collections.Generic;
using Shared.Model.Templates;
using System.Text.RegularExpressions;

namespace Lampac.Controllers.LITE
{
    public class HydraFlix : BaseENGController
    {
        [HttpGet]
        [Route("lite/hydraflix")]
        public ValueTask<ActionResult> Index(bool checksearch, long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(AppInit.conf.Hydraflix, true, checksearch, id, imdb_id, title, original_title, serial, s, rjson, chromium: true, method: "call", extension: "m3u8");
        }


        #region Video
        [HttpGet]
        [Route("lite/hydraflix/video")]
        [Route("lite/hydraflix/video.mpd")]
        [Route("lite/hydraflix/video.m3u8")]
        async public ValueTask<ActionResult> Video(long id, int s = -1, int e = -1, bool play = false)
        {
            var init = await loadKit(AppInit.conf.Hydraflix);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (id == 0)
                return OnError();

            if (PlaywrightBrowser.Status != PlaywrightStatus.NoHeadless)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/movie/{id}?autoPlay=true&theme=e1216d";
            if (s > 0)
                embed = $"{init.host}/tv/{id}/{s}/{e}?autoPlay=true&theme=e1216d";

            var cache = await black_magic(embed, init, proxyManager, proxy.data);
            if (cache.m3u8 == null)
                return StatusCode(502);

            string file = HostStreamProxy(init, cache.m3u8, proxy: proxy.proxy, headers: cache.headers);

            if (play)
                return Redirect(file);

            var headers_stream = httpHeaders(init.host, init.headers_stream);
            if (headers_stream.Count == 0)
                headers_stream = cache.headers;

            return ContentTo(VideoTpl.ToJson("play", file, "English", vast: init.vast, headers: init.streamproxy ? null : headers_stream));
        }
        #endregion

        #region black_magic
        async ValueTask<(string m3u8, List<HeadersModel> headers)> black_magic(string uri, OnlinesSettings init, ProxyManager proxyManager, (string ip, string username, string password) proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return default;

            try
            {
                string memKey = $"Hydraflix:black_magic:{uri}";
                if (!hybridCache.TryGetValue(memKey, out (string m3u8, List<HeadersModel> headers) cache))
                {
                    using (var browser = new PlaywrightBrowser())
                    {
                        var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy);
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
                                    PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
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
                        cache.m3u8 = await browser.WaitPageResult();
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

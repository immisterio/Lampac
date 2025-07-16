﻿using Lampac.Models.LITE;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine;
using Shared.Engine.CORE;
using Shared.Model.Online;
using Shared.Model.Templates;
using Shared.PlaywrightCore;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Controllers.LITE
{
    public class VidLink : BaseENGController
    {
        [HttpGet]
        [Route("lite/vidlink")]
        public ValueTask<ActionResult> Index(bool checksearch, long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(AppInit.conf.VidLink, true, checksearch, id, imdb_id, title, original_title, serial, s, rjson, mp4: true, method: "call", chromium: true);
        }


        #region Video
        [HttpGet]
        [Route("lite/vidlink/video")]
        async public ValueTask<ActionResult> Video(long id, int s = -1, int e = -1, bool play = false)
        {
            var init = await loadKit(AppInit.conf.VidLink);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (id == 0)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/movie/{id}";
            if (s > 0)
                embed = $"{init.host}/tv/{id}/{s}/{e}";

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
                string memKey = $"vidlink:black_magic:{uri}";
                if (!hybridCache.TryGetValue(memKey, out (string m3u8, List<HeadersModel> headers) cache))
                {
                    if (true) // init.priorityBrowser == "firefox" || Chromium.Status != PlaywrightStatus.NoHeadless
                    {
                        #region Firefox
                        using (var browser = new PlaywrightBrowser())
                        {
                            var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy);
                            if (page == null)
                                return default;

                            await page.RouteAsync("**/*", async route =>
                            {
                                try
                                {
                                    if (browser.IsCompleted)
                                    {
                                        PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                        await route.AbortAsync();
                                        return;
                                    }

                                    if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true, patterCache: "/api/(mercury|venus)$"))
                                        return;

                                    if (route.Request.Url.Contains("adsco.") || route.Request.Url.Contains("pubtrky.") || route.Request.Url.Contains("clarity."))
                                    {
                                        PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                        await route.AbortAsync();
                                        return;
                                    }

                                    if (route.Request.Url.Contains(".m3u") || route.Request.Url.Contains(".mp4"))
                                    {
                                        cache.headers = new List<HeadersModel>();
                                        foreach (var item in route.Request.Headers)
                                        {
                                            if (item.Key.ToLower() is "host" or "accept-encoding" or "connection" or "range")
                                                continue;

                                            cache.headers.Add(new HeadersModel(item.Key, item.Value.ToString()));
                                        }

                                        PlaywrightBase.ConsoleLog($"Playwright: SET {route.Request.Url}", cache.headers);
                                        //browser.IsCompleted = true;
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
                        #endregion
                    }
                    else
                    {
                        #region Scraping
                        using (var browser = new Scraping(uri, "\\.(m3u|mp4)", null))
                        {
                            browser.OnRequest += e =>
                            {
                                if (Regex.IsMatch(e.HttpClient.Request.Url, "(adsco|pubtrky|clarity)\\."))
                                    e.Ok(string.Empty);
                            };

                            var scrap = await browser.WaitPageResult();

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

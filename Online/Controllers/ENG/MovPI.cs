using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Engine;
using Lampac.Models.LITE;
using Lampac.Engine.CORE;
using Microsoft.Playwright;
using Shared.Model.Online;
using System;
using System.Collections.Generic;

namespace Lampac.Controllers.LITE
{
    public class MovPI : BaseENGController
    {
        [HttpGet]
        [Route("lite/movpi")]
        public Task<ActionResult> Index(bool checksearch, long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(AppInit.conf.MovPI, true, checksearch, id, imdb_id, title, original_title, serial, s, rjson);
        }


        #region Video
        [HttpGet]
        [Route("lite/movpi/video.m3u8")]
        async public Task<ActionResult> Video(long id, int s = -1, int e = -1)
        {
            var init = await loadKit(AppInit.conf.MovPI);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (id == 0)
                return OnError();

            if (Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/movie/{id}?autoPlay=true&poster=false";
            if (s > 0)
                embed = $"{init.host}/tv/{id}-{s}-{e}?autoPlay=true&poster=false";

            var cache = await black_magic(embed, init, proxy.data);
            if (cache.m3u8 == null)
                return StatusCode(502);

            return Redirect(HostStreamProxy(init, cache.m3u8, proxy: proxy.proxy, headers: cache.headers));
        }
        #endregion

        #region black_magic
        async ValueTask<(string m3u8, List<HeadersModel> headers)> black_magic(string uri, OnlinesSettings init, (string ip, string username, string password) proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return default;

            try
            {
                string memKey = $"movpi:black_magic:{uri}:{proxy.ip}";
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
                                if (route.Request.Url.Contains("api/chromium/iframe"))
                                {
                                    await route.ContinueAsync();
                                    return;
                                }

                                if (browser.IsCompleted)
                                {
                                    Console.WriteLine($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                    return;
                                }

                                if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                    return;

                                if (route.Request.Url == uri)
                                {
                                    await route.ContinueAsync(new RouteContinueOptions
                                    {
                                        Headers = httpHeaders(init, HeadersModel.Init(("referer", CrypTo.DecodeBase64("aHR0cHM6Ly93d3cuaHlkcmFmbGl4LnZpcC8=")))).ToDictionary()
                                    });
                                    return;
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

                        PlaywrightBase.GotoAsync(page, PlaywrightBase.IframeUrl(uri));

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

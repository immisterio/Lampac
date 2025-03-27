using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Engine;
using Lampac.Models.LITE;
using Microsoft.Extensions.Caching.Memory;
using Lampac.Engine.CORE;
using Microsoft.Playwright;
using Shared.Model.Online;
using System;

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

            string hls = await black_magic(embed, init, proxy.data);
            if (hls == null)
                return StatusCode(502);

            return Redirect(HostStreamProxy(init, hls, proxy: proxy.proxy));
        }
        #endregion

        #region black_magic
        async ValueTask<string> black_magic(string uri, OnlinesSettings init, (string ip, string username, string password) proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return uri;

            try
            {
                string memKey = $"movpi:black_magic:{uri}";
                if (!hybridCache.TryGetValue(memKey, out string m3u8))
                {
                    using (var browser = new Firefox())
                    {
                        var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy);
                        if (page == null)
                            return null;

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

                                if (await PlaywrightBase.AbortOrCache(memoryCache, page, route, abortMedia: true, fullCacheJS: true))
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

                        var response = await page.GotoAsync(PlaywrightBase.IframeUrl(uri));
                        if (response == null)
                            return null;

                        m3u8 = await browser.WaitPageResult();
                    }

                    if (m3u8 == null)
                        return null;

                    hybridCache.Set(memKey, m3u8, cacheTime(20, init: init));
                }

                return m3u8;
            }
            catch { return null; }
        }
        #endregion
    }
}

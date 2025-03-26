using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Engine;
using Lampac.Models.LITE;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using System;

namespace Lampac.Controllers.LITE
{
    public class TwoEmbed : BaseENGController
    {
        [HttpGet]
        [Route("lite/twoembed")]
        public Task<ActionResult> Index(bool checksearch, long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(AppInit.conf.Twoembed, true, checksearch, id, imdb_id, title, original_title, serial, s, rjson);
        }


        #region Video
        [HttpGet]
        [Route("lite/twoembed/video.m3u8")]
        async public Task<ActionResult> Video(long id, int s = -1, int e = -1)
        {
            var init = await loadKit(AppInit.conf.Twoembed);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/embed/movie/{id}";
            if (s > 0)
                embed = $"{init.host}/embed/tv/{id}/{s}/{e}";

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
                string memKey = $"twoembed:black_magic:{uri}";
                if (!memoryCache.TryGetValue(memKey, out string m3u8))
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
                                if (await PlaywrightBase.AbortOrCache(memoryCache, page, route, abortMedia: true, fullCacheJS: true))
                                    return;

                                if (browser.IsCompleted || Regex.IsMatch(route.Request.Url, "(fonts.googleapis|pixel.embed|rtmark)\\."))
                                {
                                    Console.WriteLine($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
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

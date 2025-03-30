using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Microsoft.Playwright;
using Shared.Engine;
using Lampac.Models.LITE;
using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

namespace Lampac.Controllers.LITE
{
    public class SmashyStream : BaseENGController
    {
        [HttpGet]
        [Route("lite/smashystream")]
        public Task<ActionResult> Index(bool checksearch, long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(AppInit.conf.Smashystream, true, checksearch, id, imdb_id, title, original_title, serial, s, rjson);
        }


        #region Video
        [HttpGet]
        [Route("lite/smashystream/video.m3u8")]
        async public Task<ActionResult> Video(long id, int s = -1, int e = -1)
        {
            var init = await loadKit(AppInit.conf.Smashystream);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (id == 0)
                return OnError();

            if (Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/movie/{id}";
            if (s > 0)
                embed = $"{init.host}/tv/{id}?s={s}&e={e}";

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
                string memKey = $"smashystream:black_magic:{uri}";
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
                                if (await PlaywrightBase.AbortOrCache(page, route, fullCacheJS: true))
                                    return;

                                if (browser.IsCompleted || Regex.IsMatch(route.Request.Url, "(\\.vtt|histats.com|solid.gif|poster.png|inkblotconusor\\.|jrbbavbvqmrjw\\.)"))
                                {
                                    Console.WriteLine($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                    return;
                                }

                                if (m3u8 != null || route.Request.Url.Contains(".m3u"))
                                {
                                    await route.AbortAsync();
                                    return;
                                }

                                if (route.Request.Url.Contains("master.txt"))
                                {
                                    Console.WriteLine($"Playwright: SET {route.Request.Url}");
                                    browser.IsCompleted = true;
                                    m3u8 = route.Request.Url;
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

                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                        var viewportSize = await page.EvaluateAsync<ViewportSize>("() => ({ width: window.innerWidth, height: window.innerHeight })");

                        var endTime = DateTime.Now.AddSeconds(20);
                        while (endTime > DateTime.Now && m3u8 == null)
                        {
                            int vS(int center)
                            {
                                var centerX = center / 2;
                                return Random.Shared.Next(0, 3) == 1 ? (centerX + Random.Shared.Next(1, 20)) : (centerX - Random.Shared.Next(1, 20));
                            }

                            await Task.Delay(100);
                            await page.Mouse.ClickAsync(vS(viewportSize.Width), vS(viewportSize.Height));
                        }
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

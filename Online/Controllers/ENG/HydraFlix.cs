using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Engine;
using Lampac.Models.LITE;
using System;
using Shared.PlaywrightCore;

namespace Lampac.Controllers.LITE
{
    public class HydraFlix : BaseENGController
    {
        [HttpGet]
        [Route("lite/hydraflix")]
        public Task<ActionResult> Index(bool checksearch, long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(AppInit.conf.Hydraflix, true, checksearch, id, imdb_id, title, original_title, serial, s, rjson, chromium: true);
        }


        #region Video
        [HttpGet]
        [Route("lite/hydraflix/video.m3u8")]
        async public Task<ActionResult> Video(long id, int s = -1, int e = -1)
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

            string hls = await black_magic(embed, init, proxyManager, proxy.data);
            if (hls == null)
                return StatusCode(502);

            return Redirect(HostStreamProxy(init, hls, proxy: proxy.proxy));
        }
        #endregion

        #region black_magic
        async ValueTask<string> black_magic(string uri, OnlinesSettings init, ProxyManager proxyManager, (string ip, string username, string password) proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return uri;

            try
            {
                string memKey = $"Hydraflix:black_magic:{uri}";
                if (!hybridCache.TryGetValue(memKey, out string m3u8))
                {
                    using (var browser = new PlaywrightBrowser())
                    {
                        var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy);
                        if (page == null)
                            return null;

                        await page.RouteAsync("**/*", async route =>
                        {
                            try
                            {
                                if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                    return;

                                if (browser.IsCompleted || route.Request.Url.Contains("adsco."))
                                {
                                    Console.WriteLine($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                    return;
                                }

                                if (route.Request.Url.Contains(".m3u8"))
                                {
                                    Console.WriteLine($"Playwright: SET {route.Request.Url}");
                                    browser.SetPageResult(route.Request.Url);
                                    await route.AbortAsync();
                                    return;
                                }

                                await route.ContinueAsync();
                            }
                            catch { }
                        });

                        PlaywrightBase.GotoAsync(page, uri);
                        m3u8 = await browser.WaitPageResult();
                    }

                    if (m3u8 == null)
                    {
                        proxyManager.Refresh();
                        return null;
                    }

                    proxyManager.Success();
                    hybridCache.Set(memKey, m3u8, cacheTime(20, init: init));
                }

                return m3u8;
            }
            catch { return null; }
        }
        #endregion
    }
}

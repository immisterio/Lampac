using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Engine;
using Lampac.Models.LITE;
using System;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

namespace Lampac.Controllers.LITE
{
    public class AutoEmbed : BaseENGController
    {
        [HttpGet]
        [Route("lite/autoembed")]
        public Task<ActionResult> Index(bool checksearch, long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(AppInit.conf.Autoembed, true, checksearch, id, imdb_id, title, original_title, serial, s, rjson, mp4: true);
        }


        #region Video
        [HttpGet]
        [Route("lite/autoembed/video")]
        async public Task<ActionResult> Video(long id, int s = -1, int e = -1)
        {
            var init = await loadKit(AppInit.conf.Autoembed);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/embed/movie/{id}?server=1";
            if (s > 0)
                embed = $"{init.host}/embed/tv/{id}/{s}/{e}?server=1";

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
                string memKey = $"autoembed:black_magic:{uri}";
                if (!memoryCache.TryGetValue(memKey, out string mp4))
                {
                    using (var browser = new Firefox())
                    {
                        var page = await browser.NewPageAsync("ENG", httpHeaders(init).ToDictionary(), proxy);
                        if (page == null)
                            return null;

                        await page.RouteAsync("**/*", async route =>
                        {
                            if (Regex.IsMatch(route.Request.Url, "(/ads/|vast.xml|ping.gif|fonts.googleapis\\.)"))
                            {
                                Console.WriteLine($"Playwright: Abort {route.Request.Url}");
                                await route.AbortAsync();
                                return;
                            }

                            if (/*route.Request.Url.Contains("hakunaymatata.") &&*/ route.Request.Url.Contains(".mp4"))
                            {
                                browser.completionSource.SetResult(route.Request.Url);
                                await route.AbortAsync();
                                return;
                            }

                            await PlaywrightBase.CacheOrContinue(memoryCache, page, route, abortMedia: true, fullCacheJS: true);
                        });

                        _ = page.GotoAsync(uri).ConfigureAwait(false);
                        mp4 = await browser.WaitPageResult();
                    }

                    if (mp4 == null)
                        return null;

                    memoryCache.Set(memKey, mp4, cacheTime(20, init: init));
                }

                return mp4;
            }
            catch { return null; }
        }
        #endregion
    }
}

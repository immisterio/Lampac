using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using Microsoft.Playwright;
using Shared.Engine;
using Lampac.Models.LITE;
using System;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Controllers.LITE
{
    public class Vidsrc : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/vidsrc")]
        async public Task<ActionResult> Index(string imdb_id, string title, string original_title, bool checksearch, int serial, int s = -1, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Vidsrc);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(imdb_id))
                return OnError();

            if (checksearch)
                return Content("data-json=");

            if (Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            if (serial == 1)
            {
                return OnError();
            }
            else
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);

                string hls = await black_magic($"{init.host}/embed/movie/{imdb_id}", init, proxy.data);

                mtpl.Append("1080p", HostStreamProxy(init, hls), vast: init.vast);

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
        }


        async ValueTask<string> black_magic(string uri, OnlinesSettings init, (string ip, string username, string password) proxy)
        {
            try
            {
                string memKey = $"vidsrc:black_magic:{uri}";
                if (!memoryCache.TryGetValue(memKey, out string hls))
                {
                    using (var browser = new Firefox())
                    {
                        bool goexit = false;

                        var page = await browser.NewPageAsync(proxy: proxy);
                        if (page == null)
                            return null;

                        page.Popup += async (sender, e) =>
                        {
                            await e.CloseAsync();
                        };

                        await page.RouteAsync("**/*", async route =>
                        {
                            if (goexit)
                            {
                                await route.AbortAsync();
                                return;
                            }

                            if (route.Request.Url.Contains("master.m3u8"))
                            {
                                goexit = true;
                                await route.AbortAsync();
                                browser.completionSource.SetResult(route.Request.Url);
                                return;
                            }

                            await route.ContinueAsync(new RouteContinueOptions { Headers = httpHeaders(init).ToDictionary() });
                        });

                        var response = await page.GotoAsync(uri);
                        if (response == null)
                            return null;

                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                        var viewportSize = await page.EvaluateAsync<ViewportSize>("() => ({ width: window.innerWidth, height: window.innerHeight })");

                        DateTime endTime = DateTime.Now.AddSeconds(10);
                        while(endTime > DateTime.Now)
                        {
                            if (goexit)
                                break;

                            var centerX = viewportSize.Width / 2;
                            var centerY = viewportSize.Height / 2;
                            await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(20, 50)));
                            await page.Mouse.ClickAsync(centerX, centerY);
                        }

                        hls = await browser.WaitPageResult();
                        if (string.IsNullOrEmpty(hls))
                            return null;
                    }

                    memoryCache.Set(memKey, hls, cacheTime(20, init: init));
                }

                return hls;
            }
            catch { return null; }
        }
    }
}

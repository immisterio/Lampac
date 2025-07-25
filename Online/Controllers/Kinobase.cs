﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Online;
using Shared.Engine.CORE;
using Shared.Model.Online.Kinobase;
using Microsoft.Playwright;
using Shared.Engine;
using Shared.PlaywrightCore;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shared.Models.Online.Kinobase;

namespace Lampac.Controllers.LITE
{
    public class Kinobase : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinobase")]
        async public ValueTask<ActionResult> Index(string title, int year, int s = -1, int serial = -1, string href = null, string t = null, bool rjson = false, bool similar = false)
        {
            var init = await loadKit(AppInit.conf.Kinobase);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(AppInit.conf.Kinobase);
            var proxy = proxyManager.BaseGet();

            var oninvk = new KinobaseInvoke
            (
               host,
               init,
               ongettourl => 
               {
                   if (ongettourl.Contains("/search?query="))
                       return HttpClient.Get(ongettourl, timeoutSeconds: 8, proxy: proxy.proxy, referer: init.host, httpversion: 2, headers: httpHeaders(init));

                   return black_magic(ongettourl, init, proxy.data);
               },
               (url, data) => HttpClient.Post(url, data, timeoutSeconds: 8, proxy: proxy.proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy.proxy),
               requesterror: () => proxyManager.Refresh()
            );

            #region search
            if (string.IsNullOrEmpty(href))
            {
                var search = await InvokeCache<SearchModel>($"kinobase:search:{title}:{year}", cacheTime(40, init: init), proxyManager, async res =>
                {
                    var content = await oninvk.Search(title, year);
                    if (content == null)
                        return res.Fail("search");

                    return content;
                });

                if (similar || string.IsNullOrEmpty(search.Value?.link))
                    return OnResult(search, () => rjson ? search.Value.similar.Value.ToJson() : search.Value.similar.Value.ToHtml());

                if (string.IsNullOrEmpty(search.Value?.link))
                    return OnError();

                href = search.Value?.link;
            }
            #endregion

            var cache = await InvokeCache<EmbedModel>($"kinobase:view:{href}:{proxyManager.CurrentProxyIp}", cacheTime(20, init: init), proxyManager, async res =>
            {
                var content = await oninvk.Embed(href);
                if (content == null)
                    return res.Fail("embed");

                return content;
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, title, href, s, t, rjson));
        }



        #region black_magic
        async ValueTask<string> black_magic(string uri, KinobaseSettings init, (string ip, string username, string password) proxy)
        {
            try
            {
                using (var browser = new PlaywrightBrowser())
                {
                    var page = await browser.NewPageAsync(init.plugin, proxy: proxy, headers: init.headers).ConfigureAwait(false);
                    if (page == null)
                        return null;

                    await page.Context.AddCookiesAsync(new List<Cookie>()
                    {
                        new Cookie()
                        {
                            Name = "player_settings",
                            Value = $"{(init.playerjs ? "new" : "old")}|{(init.hls ? "hls" : "mp4")}|{(init.hdr ? 1 : 0)}",
                            Domain = Regex.Match(init.host, "^https?://([^/]+)").Groups[1].Value,
                            Path = "/",
                            Expires = 2220002226
                        }
                    }).ConfigureAwait(false);

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            if (route.Request.Url.Contains("/playerjs.js"))
                            {
                                await route.FulfillAsync(new RouteFulfillOptions
                                {
                                    Body = System.IO.File.ReadAllText("data/kinobase_playerjs.js")
                                });

                                return;
                            }
                            else if (route.Request.Url.Contains("/uppod.js"))
                            {
                                await route.FulfillAsync(new RouteFulfillOptions
                                {
                                    Body = System.IO.File.ReadAllText("data/kinobase_uppod.js")
                                });

                                return;
                            }

                            if (!route.Request.Url.Contains(init.host) || route.Request.Url.Contains("/comments"))
                            {
                                await route.AbortAsync();
                                return;
                            }

                            if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                return;

                            await route.ContinueAsync();
                        }
                        catch { }
                    });

                    PlaywrightBase.GotoAsync(page, uri);

                    if (init.playerjs)
                    {
                        await page.WaitForSelectorAsync("#playerjsfile").ConfigureAwait(false);
                    }
                    else
                    {
                        await page.WaitForSelectorAsync(".uppod-media").ConfigureAwait(false);
                    }

                    string content = await page.ContentAsync().ConfigureAwait(false);

                    PlaywrightBase.WebLog("GET", uri, content, proxy);
                    return content;
                }
            }
            catch { return null; }
        }
        #endregion
    }
}

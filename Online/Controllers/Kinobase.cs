using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Shared.Models.Online.Kinobase;
using Shared.Models.Online.Settings;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class Kinobase : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinobase")]
        async public ValueTask<ActionResult> Index(string title, int year, int s = -1, int serial = -1, string href = null, string t = null, bool rjson = false, bool similar = false, string source = null, string id = null)
        {
            var init = await loadKit(AppInit.conf.Kinobase);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                return OnError();

            if (string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.ToLower() == "kinobase")
                    href = id;
            }

            var proxyManager = new ProxyManager(AppInit.conf.Kinobase);
            var proxy = proxyManager.BaseGet();

            var oninvk = new KinobaseInvoke
            (
               host,
               init,
               ongettourl => 
               {
                   if (ongettourl.Contains("/search?query="))
                       return Http.Get(ongettourl, timeoutSeconds: 8, proxy: proxy.proxy, referer: init.host, httpversion: 2, headers: httpHeaders(init));

                   return black_magic(ongettourl, init, proxy.data);
               },
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
                var content = await oninvk.Embed(href, init.playerjs);
                if (content == null)
                    return res.Fail("embed");

                return content;
            });

            return OnResult(cache, () => 
            {
                if (cache.Value.IsEmpty)
                    return ShowErrorString(cache.Value.errormsg);

                return oninvk.Html(cache.Value, title, href, s, t, rjson);
            });
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

                            if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, patterCache: "/js/(jquery|boot)\\.js"))
                                return;

                            await route.ContinueAsync();
                        }
                        catch { }
                    });

                    PlaywrightBase.GotoAsync(page, uri);
                    await browser.WaitForAnySelectorAsync(page, "#playerjsfile", ".uppod-media", ".alert").ConfigureAwait(false);

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

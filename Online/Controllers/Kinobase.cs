using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Shared.Models.Online.Kinobase;
using Shared.Models.Online.Settings;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class Kinobase : BaseOnlineController<KinobaseSettings>
    {
        public Kinobase() : base(AppInit.conf.Kinobase) { }

        [HttpGet]
        [Route("lite/kinobase")]
        async public Task<ActionResult> Index(string title, int year, int s = -1, int serial = -1, string href = null, string t = null, bool rjson = false, bool similar = false, string source = null, string id = null)
        {
            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                return OnError();

            if (await IsRequestBlocked(rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.ToLower() == "kinobase")
                    href = id;
            }

            var oninvk = new KinobaseInvoke
            (
               host,
               init,
               ongettourl => 
               {
                   if (ongettourl.Contains("/search?query="))
                       return httpHydra.Get(ongettourl, addheaders: HeadersModel.Init("referer", init.host));

                   return black_magic(ongettourl);
               },
               streamfile => HostStreamProxy(streamfile),
               requesterror: () => proxyManager?.Refresh()
            );

            #region search
            if (string.IsNullOrEmpty(href))
            {
                var search = await InvokeCacheResult<SearchModel>($"kinobase:search:{title}:{year}", 40, async e =>
                {
                    var content = await oninvk.Search(title, year);
                    if (content == null)
                        return e.Fail("search");

                    return e.Success(content);
                });

                if (similar || string.IsNullOrEmpty(search.Value?.link))
                    return await ContentTpl(search, () => search.Value.similar);

                if (string.IsNullOrEmpty(search.Value?.link))
                    return OnError();

                href = search.Value?.link;
            }
            #endregion

            var cache = await InvokeCacheResult<EmbedModel>($"kinobase:view:{href}:{proxyManager?.CurrentProxyIp}", 20, async e =>
            {
                var content = await oninvk.Embed(href, init.playerjs);
                if (content == null)
                    return e.Fail("embed");

                return e.Success(content);
            });

            if (cache.IsSuccess && cache.Value.IsEmpty)
                return ShowError(cache.Value.errormsg);

            return await ContentTpl(cache, () => oninvk.Tpl(cache.Value, title, href, s, t, rjson));
        }

        #region black_magic
        async Task<string> black_magic(string uri)
        {
            try
            {
                using (var browser = new PlaywrightBrowser())
                {
                    var page = await browser.NewPageAsync(init.plugin, proxy: proxy_data, headers: init.headers).ConfigureAwait(false);
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

                    PlaywrightBase.WebLog("GET", uri, content, proxy_data);
                    return content;
                }
            }
            catch { return null; }
        }
        #endregion
    }
}

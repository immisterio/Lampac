using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Online;
using Shared.Engine.CORE;
using Shared.Model.Online.VideoDB;
using Microsoft.Extensions.Caching.Memory;
using Shared.Model.Templates;

namespace Lampac.Controllers.LITE
{
    public class VideoDB : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/videodb")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, string t, int s = -1, int sid = -1, bool origsource = false, bool rjson = false, int serial = -1)
        {
            var init = await loadKit(AppInit.conf.VideoDB);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (kinopoisk_id == 0)
                return OnError();

            reset: var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: serial == 0 ? null : -1);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            var oninvk = new VideoDBInvoke
            (
               host,
               init.corsHost(),
               (url, head) => rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : HttpClient.Get(init.cors(url), timeoutSeconds: 8, headers: httpHeaders(init), proxy: proxy, httpversion: 2),
               streamfile => streamfile
            );

            var cache = await InvokeCache<EmbedModel>($"videodb:view:{kinopoisk_id}", cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(kinopoisk_id);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, accsArgs(string.Empty), kinopoisk_id, title, original_title, t, s, sid, rjson, rhub: rch.enable), origsource: origsource, gbcache: !rch.enable);
        }


        [HttpGet]
        [Route("lite/videodb/manifest")]
        [Route("lite/videodb/manifest.m3u8")]
        async public Task<ActionResult> Manifest(string link, bool serial)
        {
            var init = await loadKit(AppInit.conf.VideoDB);
            if (await IsBadInitialization(init))
                return badInitMsg;

            if (string.IsNullOrEmpty(link))
                return OnError();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: serial ? -1 : null);
            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            string memKey = rch.ipkey($"videodb:video:{link}", proxyManager);
            if (!memoryCache.TryGetValue(memKey, out string location))
            {
                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                if (rch.enable)
                {
                    var res = await rch.Headers(link, null, httpHeaders(init));
                    location = res.currentUrl;
                }
                else
                {
                    location = await HttpClient.GetLocation(link, httpversion: 2, proxy: proxy, headers: httpHeaders(init));
                }

                if (string.IsNullOrEmpty(location) || link == location)
                {
                    if (init.rhub && init.rhub_fallback) {
                        init.rhub = false;
                        goto reset;
                    }
                    return OnError();
                }

                if (!rch.enable)
                    proxyManager.Success();

                memoryCache.Set(memKey, location, cacheTime(20, rhub: 2, init: init));
            }

            string hls = HostStreamProxy(init, location, proxy: proxy);

            if (HttpContext.Request.Path.Value.Contains(".m3u8"))
                return Redirect(hls);

            return ContentTo(VideoTpl.ToJson("play", hls, "auto", vast: init.vast));
        }


        //static CookieParam[] cookies = null;

        //static DateTime excookies = default;

        //async ValueTask<string> black_magic(long kinopoisk_id)
        //{
        //    if (cookies != null && DateTime.Now > excookies)
        //        cookies = null;

        //    using (var browser = await PuppeteerTo.Browser())
        //    {
        //        var page = cookies != null ? await browser.Page(cookies) : await browser.Page(new Dictionary<string, string>()
        //        {
        //            ["cookie"] = "invite=a246a3f46c82fe439a45c3dbbbb24ad5;"
        //        });

        //        if (page == null)
        //            return null;

        //        if (cookies == null)
        //            await page.GoToAsync($"{AppInit.conf.VideoDB.host}/invite.php");

        //        var response = await page.GoToAsync($"view-source:{AppInit.conf.VideoDB.host}/iplayer/videodb.php?kp={kinopoisk_id}", new NavigationOptions() 
        //        { 
        //            Referer = AppInit.conf.VideoDB.host
        //        });

        //        string html = await response.TextAsync();
        //        if (!html.Contains("new Playerjs"))
        //        {
        //            cookies = null;
        //            return null;
        //        }

        //        if (cookies == null)
        //            excookies = DateTime.Now.AddMinutes(20);

        //        cookies = await page.GetCookiesAsync();

        //        return html;
        //    }
        //}
    }
}

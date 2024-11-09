using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Online;
using System.Collections.Generic;
using Shared.Model.Online;
using Shared.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class VideoDB : BaseOnlineController
    {
        List<HeadersModel> baseheader = HeadersModel.Init(
            ("cache-control", "no-cache"),
            ("dnt", "1"),
            ("origin", "https://kinoplay2.site"),
            ("pragma", "no-cache"),
            ("priority", "u=1, i"),
            ("referer", "https://kinoplay2.site/"),
            ("sec-ch-ua", "\"Google Chrome\";v=\"129\", \"Not = A ? Brand\";v=\"8\", \"Chromium\";v=\"129\""),
            ("sec-ch-ua-mobile", "?0"),
            ("sec-ch-ua-platform", "\"Windows\""),
            ("sec-fetch-dest", "empty"),
            ("sec-fetch-mode", "cors"),
            ("sec-fetch-site", "cross-site")
        );

        [HttpGet]
        [Route("lite/videodb")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, string t, int s = -1, int sid = -1, bool origsource = false, bool rjson = false)
        {
            var init = AppInit.conf.VideoDB;

            if (!init.enable || kinopoisk_id == 0)
                return OnError();

            if (init.rhub)
                return ShowError(RchClient.ErrorMsg);

            var proxyManager = new ProxyManager("videodb", init);
            var proxy = proxyManager.Get();

            var oninvk = new VideoDBInvoke
            (
               host,
               init.corsHost(),
               (url, head) => HttpClient.Get(init.cors(url), timeoutSeconds: 8, headers: httpHeaders(init, baseheader), proxy: proxy, httpversion: 2),
               streamfile => streamfile
            );

            var content = await InvokeCache($"videodb:view:{kinopoisk_id}", cacheTime(20, init: init), () => oninvk.Embed(kinopoisk_id));
            if (content?.pl == null)
                return OnError();

            if (origsource)
                return Json(content);

            return ContentTo(oninvk.Html(content, kinopoisk_id, title, original_title, t, s, sid, rjson));
        }


        [HttpGet]
        [Route("lite/videodb/manifest.m3u8")]
        async public Task<ActionResult> Manifest(string link)
        {
            var init = AppInit.conf.VideoDB;

            if (!init.enable || string.IsNullOrEmpty(link))
                return OnError();

            var proxyManager = new ProxyManager("videodb", init);
            var proxy = proxyManager.Get();

            string location = await HttpClient.GetLocation(link, httpversion: 2, proxy: proxy, headers: httpHeaders(init, baseheader));
            if (string.IsNullOrEmpty(location) || link == location)
                return OnError();

            return Redirect(HostStreamProxy(init, location, proxy: proxy, plugin: "videodb"));
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

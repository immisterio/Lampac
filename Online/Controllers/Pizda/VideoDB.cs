using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Online;
using System.Collections.Generic;
using Shared.Engine;
using PuppeteerSharp;
using System;

namespace Lampac.Controllers.LITE
{
    public class VideoDB : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/videodb")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, string t, int s = -1, int sid = -1)
        {
            var init = AppInit.conf.VideoDB;

            if (!init.enable || kinopoisk_id == 0)
                return OnError();

            var oninvk = new VideoDBInvoke
            (
               host,
               init.corsHost(),
               MaybeInHls(init.hls, init),
               (url, head) => HttpClient.Get(init.cors(url), timeoutSeconds: 8, referer: "https://www.google.com/", headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, plugin: "videodb")
            );

            string html = await InvokeCache($"videodb:view:{kinopoisk_id}", cacheTime(120, init: init), () => black_magic(kinopoisk_id));

            var content = oninvk.Embed(html);
            if (content.pl == null)
                return OnError();

            return Content(oninvk.Html(content, kinopoisk_id, title, original_title, t, s, sid), "text/html; charset=utf-8");
        }


        static CookieParam[] cookies = null;

        static DateTime excookies = default;

        async ValueTask<string> black_magic(long kinopoisk_id)
        {
            if (cookies != null && DateTime.Now > excookies)
                cookies = null;

            using (var browser = await PuppeteerTo.Browser())
            {
                var page = cookies != null ? await browser.Page(cookies) : await browser.Page(new Dictionary<string, string>()
                {
                    ["cookie"] = "invite=a246a3f46c82fe439a45c3dbbbb24ad5;"
                });

                if (page == null)
                    return null;

                if (cookies == null)
                    await page.GoToAsync($"{AppInit.conf.VideoDB.host}/invite.php");

                var response = await page.GoToAsync($"view-source:{AppInit.conf.VideoDB.host}/iplayer/videodb.php?kp={kinopoisk_id}", new NavigationOptions() 
                { 
                    Referer = AppInit.conf.VideoDB.host
                });

                string html = await response.TextAsync();
                if (!html.Contains("new Playerjs"))
                {
                    cookies = null;
                    return null;
                }

                if (cookies == null)
                    excookies = DateTime.Now.AddMinutes(20);

                cookies = await page.GetCookiesAsync();

                return html;
            }
        }
    }
}

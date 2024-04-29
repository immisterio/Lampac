using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Online;
using Shared.Engine.Online;
using System.Collections.Generic;
using PuppeteerSharp;
using Shared.Engine;
using System;
using System.Linq;
using Shared.Model.Online;

namespace Lampac.Controllers.LITE
{
    public class Zetflix : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/zetflix")]
        async public Task<ActionResult> Index(long id, int serial, long kinopoisk_id, string title, string original_title, string t, int s = -1, bool origsource = false)
        {
            var init = AppInit.conf.Zetflix;

            if (!init.enable || kinopoisk_id == 0)
                return OnError();

            var oninvk = new ZetflixInvoke
            (
               host,
               init.corsHost(),
               MaybeInHls(init.hls, init),
               (url, head) => HttpClient.Get(init.cors(url), headers: httpHeaders(init, head), timeoutSeconds: 8),
               onstreamtofile => HostStreamProxy(init, onstreamtofile, plugin: "zetflix")
               //AppInit.log
            );

            int rs = serial == 1 ? (s == -1 ? 1 : s) : s;

            string html = await InvokeCache($"zetfix:view:{kinopoisk_id}:{rs}", cacheTime(40, init: init), async () => 
            {
                string uri = $"{AppInit.conf.Zetflix.host}/iplayer/videodb.php?kp={kinopoisk_id}" + (rs > 0 ? $"&season={rs}" : "");

                if (init.black_magic)
                    return await black_magic(uri);

                string html = string.IsNullOrEmpty(PHPSESSID) ? null : await HttpClient.Get(uri, cookie: $"PHPSESSID={PHPSESSID}", headers: HeadersModel.Init("Referer", "https://www.google.com/"));
                if (html != null && !html.StartsWith("<script>(function"))
                {
                    if (!html.Contains("new Playerjs"))
                        return null;

                    return html;
                }

                using (var browser = await PuppeteerTo.Browser())
                {
                    var page = await browser.Page(cookies, new Dictionary<string, string>()
                    {
                        ["Referer"] = "https://www.google.com/"
                    });

                    if (page == null)
                        return null;

                    await page.GoToAsync(uri, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded } });

                    var response = await page.GoToAsync($"view-source:{uri}");
                    html = await response.TextAsync();

                    if (html.StartsWith("<script>(function"))
                        return null;

                    var cook = await page.GetCookiesAsync();
                    PHPSESSID = cook?.FirstOrDefault(i => i.Name == "PHPSESSID")?.Value;

                    if (!html.Contains("new Playerjs"))
                        return null;

                    return html;
                }
            });

            if (html == null)
                return OnError();

            if (origsource)
                return Content(html, "text/plain; charset=utf-8");

            var content = oninvk.Embed(html);
            if (content.pl == null)
                return OnError();

            int number_of_seasons = 1;
            if (!content.movie && s == -1 && id > 0)
                number_of_seasons = await InvokeCache($"zetfix:number_of_seasons:{kinopoisk_id}", cacheTime(120, init: init), () => oninvk.number_of_seasons(id));

            return Content(oninvk.Html(content, number_of_seasons, kinopoisk_id, title, original_title, t, s), "text/html; charset=utf-8");
        }


        static string PHPSESSID = null;

        static CookieParam[] cookies = null;

        static DateTime excookies = default;

        async ValueTask<string> black_magic(string uri)
        {
            if (cookies != null && DateTime.Now > excookies)
                cookies = null;

            using (var browser = await PuppeteerTo.Browser())
            {
                var page = await browser.Page(cookies, new Dictionary<string, string>()
                {
                    ["Referer"] = "https://www.google.com/"
                });

                if (page == null)
                    return null;

                var response = await page.GoToAsync($"view-source:{uri}");
                string html = await response.TextAsync();

                if (html.StartsWith("<script>(function(){"))
                {
                    cookies = null;
                    await page.DeleteCookieAsync();
                    await page.GoToAsync(uri, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded } });

                    response = await page.GoToAsync($"view-source:{uri}");
                    html = await response.TextAsync();
                }

                if (html.StartsWith("<script>(function"))
                    return null;

                if (cookies == null)
                    excookies = DateTime.Now.AddMinutes(10);

                cookies = await page.GetCookiesAsync();

                if (!html.Contains("new Playerjs"))
                    return null;

                return html;
            }
        }
    }
}

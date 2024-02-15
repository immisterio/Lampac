using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Online;
using System.Collections.Generic;
using Shared.Engine;
using System.Linq;
using Shared.Model.Online;
using System;

namespace Lampac.Controllers.LITE
{
    public class VideoDB : BaseOnlineController
    {
        static string cookie = null, userAgent = null;

        [HttpGet]
        [Route("lite/videodb")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, string t, int s = -1, int sid = -1)
        {
            var init = AppInit.conf.VideoDB;

            if (!init.enable || kinopoisk_id == 0)
                return OnError();

            if (cookie == null)
            {
                if (await updateCookie(kinopoisk_id) == false)
                    return OnError();
            }

            var oninvk = new VideoDBInvoke
            (
               host,
               init.corsHost(),
               init.hls,
               (url, head) => HttpClient.Get(init.cors(url), timeoutSeconds: 8, cookie: cookie, referer: "https://www.google.com/", addHeaders: HeadersModel.Init(("User-Agent", userAgent))),
               streamfile => HostStreamProxy(init, streamfile, plugin: "videodb")
            );

            var content = await InvokeCache($"videodb:view:{kinopoisk_id}", cacheTime(120), async () => 
            {
                var res = await oninvk.Embed(kinopoisk_id);
                if (res.obfuscation)
                {
                    if (await updateCookie(kinopoisk_id))
                        res = await oninvk.Embed(kinopoisk_id);
                }

                return res;
            });

            if (content.pl == null)
                return OnError();

            return Content(oninvk.Html(content, kinopoisk_id, title, original_title, t, s, sid), "text/html; charset=utf-8");
        }


        static DateTime nextUpdate;

        async ValueTask<bool> updateCookie(long kinopoisk_id)
        {
            if (nextUpdate > DateTime.Now)
                return false;

            cookie = null;   
            nextUpdate = DateTime.Now.AddMinutes(2);

            using (var browser = await PuppeteerTo.Browser())
            {
                userAgent = await browser.GetUserAgentAsync();

                using (var page = await PuppeteerTo.Page(browser, new Dictionary<string, string>()
                {
                    ["Referer"] = "https://www.google.com/"
                }))
                {
                    await page.GoToAsync($"{AppInit.conf.VideoDB.host}/iplayer/videodb.php?kp={kinopoisk_id}");
                    string PHPSESSID = (await page.GetCookiesAsync())?.FirstOrDefault(i => i.Name == "PHPSESSID")?.Value;

                    if (!string.IsNullOrEmpty(PHPSESSID))
                    {
                        cookie = $"PHPSESSID={PHPSESSID};";
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

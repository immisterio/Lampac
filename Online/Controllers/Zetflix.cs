using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Online;
using Shared.Engine.Online;
using System.Collections.Generic;
using Shared.Engine;
using System;
using System.Linq;
using Shared.Model.Online;

namespace Lampac.Controllers.LITE
{
    public class Zetflix : BaseOnlineController
    {
        static string PHPSESSID = null;

        [HttpGet]
        [Route("lite/zetflix")]
        async public Task<ActionResult> Index(long id, int serial, long kinopoisk_id, string title, string original_title, string t, int s = -1, bool orightml = false, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Zetflix);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (kinopoisk_id == 0)
                return OnError();

            string log = $"{HttpContext.Request.Path.Value}\n\nstart init\n";

            var oninvk = new ZetflixInvoke
            (
               host,
               init.corsHost(),
               MaybeInHls(init.hls, init),
               (url, head) => HttpClient.Get(init.cors(url), headers: httpHeaders(init, head), timeoutSeconds: 8),
               onstreamtofile => HostStreamProxy(init, onstreamtofile)
               //AppInit.log
            );

            int rs = serial == 1 ? (s == -1 ? 1 : s) : s;

            string html = await InvokeCache($"zetfix:view:{kinopoisk_id}:{rs}", cacheTime(20, init: init), async () => 
            {
                string uri = $"{AppInit.conf.Zetflix.host}/iplayer/videodb.php?kp={kinopoisk_id}" + (rs > 0 ? $"&season={rs}" : "");

                string html = string.IsNullOrEmpty(PHPSESSID) ? null : await HttpClient.Get(uri, cookie: $"PHPSESSID={PHPSESSID}", headers: HeadersModel.Init("Referer", "https://www.google.com/"));
                if (html != null && !html.StartsWith("<script>(function"))
                {
                    if (!html.Contains("new Playerjs"))
                        return null;

                    return html;
                }

                try
                {
                    using (var browser = new Chromium())
                    {
                        log += "browser init\n";

                        var page = await browser.NewPageAsync(new Dictionary<string, string>()
                        {
                            ["Referer"] = "https://www.google.com/"

                        });

                        if (page == null)
                            return null;

                        log += "page init\n";

                        await page.GotoAsync(uri);
                        var cook = await page.Context.CookiesAsync();
                        PHPSESSID = cook?.FirstOrDefault(i => i.Name == "PHPSESSID")?.Value;
                        if (!string.IsNullOrEmpty(PHPSESSID))
                        {
                            html = await HttpClient.Get(uri, cookie: $"PHPSESSID={PHPSESSID}", headers: HeadersModel.Init("Referer", "https://www.google.com/"));
                            if (html != null && !html.StartsWith("<script>(function"))
                            {
                                if (!html.Contains("new Playerjs"))
                                    return null;

                                return html;
                            }
                        }

                        return null;
                    }
                }
                catch (Exception ex) 
                {
                    log += $"\nex: {ex}\n";
                    return null; 
                }
            });

            if (html == null)
                return OnError();

            if (orightml)
                return Content(html, "text/plain; charset=utf-8");

            var content = oninvk.Embed(html);
            if (content.pl == null)
                return OnError();

            if (origsource)
                return Json(content);

            int number_of_seasons = 1;
            if (!content.movie && s == -1 && id > 0)
                number_of_seasons = await InvokeCache($"zetfix:number_of_seasons:{kinopoisk_id}", cacheTime(120, init: init), () => oninvk.number_of_seasons(id));

            OnLog(log + "\nStart OnResult");

            return ContentTo(oninvk.Html(content, number_of_seasons, kinopoisk_id, title, original_title, t, s, vast: init.vast, rjson: rjson));
        }
    }
}

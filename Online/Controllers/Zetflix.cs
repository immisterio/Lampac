using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using Online;
using Shared.Engine.Online;
using System.Collections.Generic;

namespace Lampac.Controllers.LITE
{
    public class Zetflix : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/zetflix")]
        async public Task<ActionResult> Index(long id, long kinopoisk_id, string title, string original_title, string t, int s = -1)
        {
            var init = AppInit.conf.Zetflix;

            if (!init.enable || kinopoisk_id == 0)
                return OnError();

            ProxyManager proxyManager = new ProxyManager("zetflix", init);
            var proxy = proxyManager.Get();

            var oninvk = new ZetflixInvoke
            (
               host,
               init.corsHost(),
               init.hls,
               (url, head) => HttpClient.Get(init.cors(url), addHeaders: init.headers ?? head, timeoutSeconds: 8, proxy: proxy),
               onstreamtofile => HostStreamProxy(init, onstreamtofile, proxy: proxy, plugin: "zetflix")
               //AppInit.log
            );

            string html = await InvokeCache($"zetfix:view:{kinopoisk_id}:{s}", cacheTime(120), () => black_magic(kinopoisk_id, s), proxyManager);
            if (html == null)
                return OnError();

            var content = oninvk.Embed(html);
            if (content.pl == null)
                return OnError();

            int number_of_seasons = 1;
            if (!content.movie && s == -1 && id > 0)
                number_of_seasons = await InvokeCache($"zetfix:number_of_seasons:{kinopoisk_id}", cacheTime(60), () => oninvk.number_of_seasons(id));

            return Content(oninvk.Html(content, number_of_seasons, kinopoisk_id, title, original_title, t, s), "text/html; charset=utf-8");
        }


        async ValueTask<string> black_magic(long kinopoisk_id, int s)
        {
            var page = await AppInit.BrowserPage("zetflix", new Dictionary<string, string>()
            {
                ["Referer"] = "https://www.google.com/"
            });

            string uri = $"{AppInit.conf.Zetflix.host}/iplayer/videodb.php?kp={kinopoisk_id}" + (s > 0 ? $"&season={s}" : "");

            var response = await page.GoToAsync($"view-source:{uri}");
            string html = await response.TextAsync();

            if (html.StartsWith("<script>(function(){"))
            {
                await page.DeleteCookieAsync();
                await page.GoToAsync(uri);

                //reskld = await page.ReloadAsync();
                response = await page.GoToAsync($"view-source:{uri}");
                html = await response.TextAsync();
            }

            if (!html.Contains("new Playerjs"))
                return null;

            return html;
        }
    }
}

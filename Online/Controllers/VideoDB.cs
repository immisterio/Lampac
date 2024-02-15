using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using System.Collections.Generic;

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

            var proxyManager = new ProxyManager("videodb", AppInit.conf.VideoDB);
            var proxy = proxyManager.Get();

            var oninvk = new VideoDBInvoke
            (
               host,
               init.corsHost(),
               init.hls,
               (url, head) => HttpClient.Get(init.cors(url), timeoutSeconds: 8, proxy: proxy, addHeaders: init.headers ?? head),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "videodb")
            );

            string html = await InvokeCache($"videodb:view:{kinopoisk_id}", cacheTime(120), () => black_magic(kinopoisk_id));
            if (html == null)
                return OnError();

            var content = oninvk.Embed(html);
            if (content.pl == null)
                return OnError();

            return Content(oninvk.Html(content, kinopoisk_id, title, original_title, t, s, sid), "text/html; charset=utf-8");
        }


        async ValueTask<string> black_magic(long kinopoisk_id)
        {
            using (var page = await AppInit.BrowserPage(new Dictionary<string, string>()
            {
                ["Referer"] = "https://www.google.com/"
            }))
            {
                string uri = $"{AppInit.conf.VideoDB.host}/iplayer/videodb.php?kp={kinopoisk_id}";

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

                await page.CloseAsync();

                if (!html.Contains("new Playerjs"))
                    return null;

                return html;
            }
        }
    }
}

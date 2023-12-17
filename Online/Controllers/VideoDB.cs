using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;

namespace Lampac.Controllers.LITE
{
    public class VideoDB : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/videodb")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int serial, string t, int s = -1, int sid = -1)
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
               (url, head) => HttpClient.Get(init.cors(url), timeoutSeconds: 8, proxy: proxy, addHeaders: head),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "videodb")
            );

            var content = await InvokeCache($"videodb:view:{kinopoisk_id}", cacheTime(20), () => oninvk.Embed(kinopoisk_id, serial));
            if (content.pl == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(content, kinopoisk_id, title, original_title, t, s, sid), "text/html; charset=utf-8");
        }
    }
}

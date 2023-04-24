using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Shared.Engine.Online;

namespace Lampac.Controllers.LITE
{
    public class VideoCDN : BaseController
    {
        [HttpGet]
        [Route("lite/vcdn")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, string t, int s = -1)
        {
            if (!AppInit.conf.VCDN.enable)
                return Content(string.Empty);

            System.Net.WebProxy proxy = null;
            if (AppInit.conf.VCDN.useproxy)
                proxy = HttpClient.webProxy();

            var oninvk = new VideoCDNInvoke
            (
               host,
               AppInit.conf.VCDN.corsHost(),
               (url, referer) => HttpClient.Get(AppInit.conf.VCDN.corsHost(url), referer: referer, timeoutSeconds: 8, proxy: proxy),
               streamfile => HostStreamProxy(AppInit.conf.VCDN.streamproxy, streamfile)
            );

            var content = await InvokeCache($"videocdn:view:{imdb_id}:{kinopoisk_id}", AppInit.conf.multiaccess ? 20 : 5, () => oninvk.Embed(kinopoisk_id, imdb_id));
            if (content == null)
                return Content(string.Empty);

            return Content(oninvk.Html(content, imdb_id, kinopoisk_id, title, original_title, t, s), "text/html; charset=utf-8");
        }
    }
}

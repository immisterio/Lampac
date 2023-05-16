using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;

namespace Lampac.Controllers.LITE
{
    public class VideoCDN : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/vcdn")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, string t, int s = -1)
        {
            if (!AppInit.conf.VCDN.enable || (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id)))
                return Content(string.Empty);

            var proxyManager = new ProxyManager("vcdn", AppInit.conf.VCDN);
            var proxy = proxyManager.Get();

            var oninvk = new VideoCDNInvoke
            (
               host,
               AppInit.conf.VCDN.corsHost(),
               (url, referer) => HttpClient.Get(AppInit.conf.VCDN.corsHost(url), referer: referer, timeoutSeconds: 8, proxy: proxy),
               streamfile => HostStreamProxy(AppInit.conf.VCDN, streamfile, proxy: proxy)
            );

            var content = await InvokeCache($"videocdn:view:{imdb_id}:{kinopoisk_id}", AppInit.conf.multiaccess ? 20 : 5, () => oninvk.Embed(kinopoisk_id, imdb_id));
            if (content == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(content, imdb_id, kinopoisk_id, title, original_title, t, s), "text/html; charset=utf-8");
        }
    }
}

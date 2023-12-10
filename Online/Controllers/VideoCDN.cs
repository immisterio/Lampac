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
            if (!AppInit.conf.VCDN.enable)
                return OnError("disable");

            var proxyManager = new ProxyManager("vcdn", AppInit.conf.VCDN);
            var proxy = proxyManager.Get();

            var oninvk = new VideoCDNInvoke
            (
               host,
               AppInit.conf.VCDN.corsHost(),
               AppInit.conf.VCDN.corsHost(AppInit.conf.VCDN.apihost),
               AppInit.conf.VCDN.token,
               (url, referer) => HttpClient.Get(AppInit.conf.VCDN.corsHost(url), referer: referer, timeoutSeconds: 8, proxy: proxy),
               streamfile => HostStreamProxy(AppInit.conf.VCDN, streamfile, proxy: proxy, plugin: "vcdn")
            );

            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
            {
                string similars = await InvokeCache($"videocdn:search:{title}:{original_title}", cacheTime(40), () => oninvk.Search(title, original_title));
                if (string.IsNullOrEmpty(similars))
                    return OnError("similars");

                return Content(similars, "text/html; charset=utf-8");
            }

            var content = await InvokeCache($"videocdn:view:{imdb_id}:{kinopoisk_id}", cacheTime(20), () => oninvk.Embed(kinopoisk_id, imdb_id));
            if (content == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(content, imdb_id, kinopoisk_id, title, original_title, t, s), "text/html; charset=utf-8");
        }
    }
}

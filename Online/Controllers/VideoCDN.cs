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
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, string t, int s = -1, int serial = -1)
        {
            var init = AppInit.conf.VCDN;

            if (!init.enable)
                return OnError();

            var proxyManager = new ProxyManager("vcdn", init);
            var proxy = proxyManager.Get();

            var oninvk = new VideoCDNInvoke
            (
               host,
               init.corsHost(),
               init.cors(init.apihost),
               init.token,
               MaybeInHls(init.hls, init),
               (url, referer) => HttpClient.Get(init.cors(url), referer: referer, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "vcdn")
            );

            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
            {
                string similars = await InvokeCache($"videocdn:search:{title}:{original_title}", cacheTime(40), () => oninvk.Search(title, original_title, serial));
                if (string.IsNullOrEmpty(similars))
                    return OnError("similars");

                return Content(similars, "text/html; charset=utf-8");
            }

            var content = await InvokeCache($"videocdn:view:{imdb_id}:{kinopoisk_id}:{proxyManager.CurrentProxyIp}", cacheTime(20), () => oninvk.Embed(kinopoisk_id, imdb_id), proxyManager);
            if (content == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(content, imdb_id, kinopoisk_id, title, original_title, t, s), "text/html; charset=utf-8");
        }
    }
}

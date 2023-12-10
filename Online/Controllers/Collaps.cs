using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;

namespace Lampac.Controllers.LITE
{
    public class Collaps : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/collaps")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int s = -1)
        {
            if (!AppInit.conf.Collaps.enable || (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id)))
                return OnError();

            var proxyManager = new ProxyManager("collaps", AppInit.conf.Collaps);
            var proxy = proxyManager.Get();

            var oninvk = new CollapsInvoke
            (
               host,
               AppInit.conf.Collaps.corsHost(),
               ongettourl => HttpClient.Get(AppInit.conf.Collaps.corsHost(ongettourl), timeoutSeconds: 8, proxy: proxy),
               onstreamtofile => HostStreamProxy(AppInit.conf.Collaps, onstreamtofile, proxy: proxy)
            );

            var content = await InvokeCache($"collaps:view:{imdb_id}:{kinopoisk_id}", cacheTime(20), () => oninvk.Embed(imdb_id, kinopoisk_id));
            if (content == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(content, imdb_id, kinopoisk_id, title, original_title, s), "text/html; charset=utf-8");
        }
    }
}

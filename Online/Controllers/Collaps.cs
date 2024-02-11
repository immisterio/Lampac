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
            var init = AppInit.conf.Collaps;

            if (!init.enable)
                return OnError();

            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return OnError();

            var proxyManager = new ProxyManager("collaps", init);
            var proxy = proxyManager.Get();

            var oninvk = new CollapsInvoke
            (
               host,
               init.corsHost(),
               init.dash,
               ongettourl => HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy),
               onstreamtofile => HostStreamProxy(init, onstreamtofile, proxy: proxy, plugin: "collaps")
            );

            var content = await InvokeCache($"collaps:view:{imdb_id}:{kinopoisk_id}", cacheTime(20), () => oninvk.Embed(imdb_id, kinopoisk_id), proxyManager);
            if (content == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(content, imdb_id, kinopoisk_id, title, original_title, s), "text/html; charset=utf-8");
        }
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using Online;
using Shared.Engine.Online;

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
               (url, head) => HttpClient.Get(init.cors(url), addHeaders: head, timeoutSeconds: 8, proxy: proxy),
               onstreamtofile => HostStreamProxy(init, onstreamtofile, proxy: proxy, plugin: "zetflix")
               //AppInit.log
            );

            var content = await InvokeCache($"zetfix:view:{kinopoisk_id}:{s}", cacheTime(20), () => oninvk.Embed(kinopoisk_id, s));
            if (content?.pl == null)
                return OnError(proxyManager);

            int number_of_seasons = 1;
            if (!content.movie && s == -1 && id > 0)
                number_of_seasons = await InvokeCache($"zetfix:number_of_seasons:{kinopoisk_id}", cacheTime(60), () => oninvk.number_of_seasons(id));

            return Content(oninvk.Html(content, number_of_seasons, kinopoisk_id, title, original_title, t, s), "text/html; charset=utf-8");
        }
    }
}

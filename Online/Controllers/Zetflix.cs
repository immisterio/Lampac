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
            if (kinopoisk_id == 0 || !AppInit.conf.Zetflix.enable)
                return OnError();

            ProxyManager proxyManager = new ProxyManager("zetflix", AppInit.conf.Zetflix);
            var proxy = proxyManager.Get();

            var oninvk = new ZetflixInvoke
            (
               host,
               AppInit.conf.Zetflix.corsHost(),
               (url, head) => HttpClient.Get(AppInit.conf.Zetflix.corsHost(url), addHeaders: head, timeoutSeconds: 8, proxy: proxy),
               onstreamtofile => HostStreamProxy(AppInit.conf.Zetflix, onstreamtofile, proxy: proxy, plugin: "zetflix")
               //AppInit.log
            );

            var content = await InvokeCache($"zetfix:view:{kinopoisk_id}:{s}", AppInit.conf.multiaccess ? 20 : 5, () => oninvk.Embed(kinopoisk_id, s));
            if (content?.pl == null)
                return OnError(proxyManager);

            int number_of_seasons = 1;
            if (!content.movie && s == -1 && id > 0)
                number_of_seasons = await InvokeCache($"zetfix:number_of_seasons:{kinopoisk_id}", 60, () => oninvk.number_of_seasons(id));

            return Content(oninvk.Html(content, number_of_seasons, kinopoisk_id, title, original_title, t, s), "text/html; charset=utf-8");
        }
    }
}

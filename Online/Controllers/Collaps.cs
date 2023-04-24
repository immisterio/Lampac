using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Shared.Engine.Online;

namespace Lampac.Controllers.LITE
{
    public class Collaps : BaseController
    {
        [HttpGet]
        [Route("lite/collaps")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int s)
        {
            if (!AppInit.conf.Collaps.enable)
                return Content(string.Empty);

            System.Net.WebProxy proxy = null;
            if (AppInit.conf.Collaps.useproxy)
                proxy = HttpClient.webProxy();

            var oninvk = new CollapsInvoke
            (
               host,
               AppInit.conf.Collaps.corsHost(),
               ongettourl => HttpClient.Get(AppInit.conf.Collaps.corsHost(ongettourl), timeoutSeconds: 8, proxy: proxy),
               onstreamtofile => HostStreamProxy(AppInit.conf.Collaps.streamproxy, onstreamtofile)
            );

            var content = await InvokeCache($"collaps:view:{imdb_id}:{kinopoisk_id}", AppInit.conf.multiaccess ? 20 : 10, () => oninvk.Embed(imdb_id, kinopoisk_id));
            if (content == null)
                return Content(string.Empty);

            return Content(oninvk.Html(content, imdb_id, kinopoisk_id, title, original_title, s), "text/html; charset=utf-8");
        }
    }
}

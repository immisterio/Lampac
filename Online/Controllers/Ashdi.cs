using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class Ashdi : BaseController
    {
        [HttpGet]
        [Route("lite/ashdi")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t = -1, int s = -1)
        {
            if (kinopoisk_id == 0 || !AppInit.conf.Ashdi.enable)
                return Content(string.Empty);

            var proxyManager = new ProxyManager("ashdi", AppInit.conf.Ashdi);
            var proxy = proxyManager.Get();

            var oninvk = new AshdiInvoke
            (
               host,
               AppInit.conf.Ashdi.corsHost(),
               ongettourl => HttpClient.Get(AppInit.conf.Ashdi.corsHost(ongettourl), timeoutSeconds: 8, proxy: proxy),
               streamfile => HostStreamProxy(AppInit.conf.Ashdi.streamproxy, streamfile)
            );

            var content = await InvokeCache($"ashdi:view:{kinopoisk_id}", AppInit.conf.multiaccess ? 40 : 10, () => oninvk.Embed(kinopoisk_id));
            if (content == null)
            {
                proxyManager.Refresh();
                return Content(string.Empty);
            }

            return Content(oninvk.Html(content, kinopoisk_id, title, original_title, t, s), "text/html; charset=utf-8");
        }
    }
}

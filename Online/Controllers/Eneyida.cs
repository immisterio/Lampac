using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class Eneyida : BaseController
    {
        [HttpGet]
        [Route("lite/eneyida")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, int t = -1, int s = -1, string href = null)
        {
            if (!AppInit.conf.Eneyida.enable)
                return Content(string.Empty);

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(original_title) || year == 0))
                return Content(string.Empty);

            var proxyManager = new ProxyManager("eneyida", AppInit.conf.Eneyida);
            var proxy = proxyManager.Get();

            var oninvk = new EneyidaInvoke
            (
               host,
               AppInit.conf.Eneyida.corsHost(),
               ongettourl => HttpClient.Get(AppInit.conf.Eneyida.corsHost(ongettourl), timeoutSeconds: 8, proxy: proxy),
               (url, data) => HttpClient.Post(AppInit.conf.Eneyida.corsHost(url), data, timeoutSeconds: 8, proxy: proxy),
               onstreamtofile => HostStreamProxy(AppInit.conf.Eneyida.streamproxy, onstreamtofile)
            );

            string search_title = clarification == 1 ? title : original_title;
            var result = await InvokeCache($"eneyida:view:{search_title}:{year}:{href}", AppInit.conf.multiaccess ? 40 : 10, () => oninvk.Embed(search_title, year, href));
            if (result == null)
            {
                proxyManager.Refresh();
                return Content(string.Empty);
            }

            return Content(oninvk.Html(result, clarification, title, original_title, year, t, s, href), "text/html; charset=utf-8");
        }
    }
}

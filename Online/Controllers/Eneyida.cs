using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Shared.Engine.Online;

namespace Lampac.Controllers.LITE
{
    public class Eneyida : BaseController
    {
        [HttpGet]
        [Route("lite/eneyida")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, string original_language, int year, int t = -1, int s = -1, string href = null)
        {
            if (!AppInit.conf.Eneyida.enable)
                return Content(string.Empty);

            System.Net.WebProxy proxy = null;
            if (AppInit.conf.Eneyida.useproxy)
                proxy = HttpClient.webProxy();

            var oninvk = new EneyidaInvoke
            (
               host,
               AppInit.conf.Eneyida.corsHost(),
               ongettourl => HttpClient.Get(AppInit.conf.Eneyida.corsHost(ongettourl), timeoutSeconds: 8, proxy: proxy),
               (url, data) => HttpClient.Post(AppInit.conf.Eneyida.corsHost(url), data, timeoutSeconds: 8, proxy: proxy),
               onstreamtofile => HostStreamProxy(AppInit.conf.Eneyida.streamproxy, onstreamtofile)
            );

            var result = await InvokeCache($"eneyida:view:{original_title}:{year}:{href}:{clarification}", AppInit.conf.multiaccess ? 40 : 10, () => oninvk.Embed(clarification == 1 ? title : original_title, year, href));

            return Content(oninvk.Html(result, clarification, title, original_title, year, t, s, href), "text/html; charset=utf-8");
        }
    }
}

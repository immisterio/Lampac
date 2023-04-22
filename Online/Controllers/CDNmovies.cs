using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Shared.Engine.Online;

namespace Lampac.Controllers.LITE
{
    public class CDNmovies : BaseController
    {
        [HttpGet]
        [Route("lite/cdnmovies")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t, int s = -1, int sid = -1)
        {
            if (!AppInit.conf.CDNmovies.enable || kinopoisk_id == 0)
                return Content(string.Empty);

            System.Net.WebProxy proxy = null;
            if (AppInit.conf.CDNmovies.useproxy)
                proxy = HttpClient.webProxy();

            var oninvk = new CDNmoviesInvoke
            (
               host,
               AppInit.conf.CDNmovies.corsHost(),
               ongettourl => HttpClient.Get(AppInit.conf.CDNmovies.corsHost(ongettourl), timeoutSeconds: 8, proxy: proxy, addHeaders: new List<(string name, string val)>()
               {
                   ("DNT", "1"),
                   ("Upgrade-Insecure-Requests", "1")
               }),
               onstreamtofile => HostStreamProxy(AppInit.conf.CDNmovies.streamproxy, onstreamtofile)
            );

            var voices = await InvokeCache($"cdnmovies:view:{kinopoisk_id}", AppInit.conf.multiaccess ? 20 : 10, () => oninvk.Embed(kinopoisk_id));
            if (voices == null)
                return Content(string.Empty);

            return Content(oninvk.Html(voices, kinopoisk_id, title, original_title, t, s, sid), "text/html; charset=utf-8");
        }
    }
}

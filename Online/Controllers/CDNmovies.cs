using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;

namespace Lampac.Controllers.LITE
{
    public class CDNmovies : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/cdnmovies")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t, int s = -1, int sid = -1)
        {
            if (!AppInit.conf.CDNmovies.enable || kinopoisk_id == 0)
                return OnError();

            var proxyManager = new ProxyManager("cdnmovies", AppInit.conf.CDNmovies);
            var proxy = proxyManager.Get();

            var oninvk = new CDNmoviesInvoke
            (
               host,
               AppInit.conf.CDNmovies.corsHost(),
               ongettourl => HttpClient.Get(AppInit.conf.CDNmovies.corsHost(ongettourl), timeoutSeconds: 8, proxy: proxy, addHeaders: new List<(string name, string val)>()
               {
                   ("DNT", "1"),
                   ("Upgrade-Insecure-Requests", "1")
               }),
               onstreamtofile => HostStreamProxy(AppInit.conf.CDNmovies, onstreamtofile, proxy: proxy)
            );

            var voices = await InvokeCache($"cdnmovies:view:{kinopoisk_id}", cacheTime(20), () => oninvk.Embed(kinopoisk_id));
            if (voices == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(voices, kinopoisk_id, title, original_title, t, s, sid), "text/html; charset=utf-8");
        }
    }
}

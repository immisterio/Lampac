using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Online;

namespace Lampac.Controllers.LITE
{
    public class CDNmovies : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/cdnmovies")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t, int s = -1, int sid = -1)
        {
            var init = AppInit.conf.CDNmovies;

            if (!init.enable || kinopoisk_id == 0)
                return OnError();

            var proxyManager = new ProxyManager("cdnmovies", init);
            var proxy = proxyManager.Get();

            var oninvk = new CDNmoviesInvoke
            (
               host,
               init.corsHost(),
               ongettourl => HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init, HeadersModel.Init(
                   ("DNT", "1"),
                   ("Upgrade-Insecure-Requests", "1")
               ))),
               onstreamtofile => HostStreamProxy(init, onstreamtofile, proxy: proxy)
            );

            var voices = await InvokeCache($"cdnmovies:view:{kinopoisk_id}", cacheTime(20), () => oninvk.Embed(kinopoisk_id), proxyManager);
            if (voices == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(voices, kinopoisk_id, title, original_title, t, s, sid), "text/html; charset=utf-8");
        }
    }
}

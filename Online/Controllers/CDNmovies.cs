using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Online;
using System.Collections.Generic;
using Lampac.Models.LITE.CDNmovies;

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

            var rch = new RchClient(HttpContext, host, init.rhub);
            var proxyManager = new ProxyManager("cdnmovies", init);
            var proxy = proxyManager.Get();

            var oninvk = new CDNmoviesInvoke
            (
               host,
               init.corsHost(),
               ongettourl => init.rhub ? rch.Get(init.cors(ongettourl)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init, HeadersModel.Init(
                   ("DNT", "1"),
                   ("Upgrade-Insecure-Requests", "1")
               ))),
               onstreamtofile => HostStreamProxy(init, onstreamtofile, proxy: proxy),
               requesterror: () => proxyManager.Refresh()
            );

            var cache = await InvokeCache<List<Voice>>($"cdnmovies:view:{kinopoisk_id}", cacheTime(20, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(kinopoisk_id);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, kinopoisk_id, title, original_title, t, s, sid));
        }
    }
}

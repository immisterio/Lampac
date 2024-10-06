using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using Shared.Model.Online.VDBmovies;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using System.Collections.Generic;

namespace Lampac.Controllers.LITE
{
    public class FanCDN : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/fancdn")]
        async public Task<ActionResult> Index(string title, string original_title, long kinopoisk_id)
        {
            var init = AppInit.conf.FanCDN.Clone();

            if (!init.enable || kinopoisk_id == 0)
                return OnError();

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            reset: var rch = new RchClient(HttpContext, host, init.rhub);
            var proxyManager = new ProxyManager("fancdn", init);
            var proxy = proxyManager.Get();

            var oninvk = new FanCDNInvoke
            (
               host,
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "fancdn")
            );

            var cache = await InvokeCache<List<Episode>>(rch.ipkey($"fancdn:{kinopoisk_id}", proxyManager), cacheTime(20, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string uri = $"{init.corsHost()}/ember/{kinopoisk_id}";

                return oninvk.Embed(init.rhub ? await rch.Get(uri) : await HttpClient.Get(uri, proxy: proxy, referer: "https://fanserialstv.net"));
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, title, original_title));
        }
    }
}

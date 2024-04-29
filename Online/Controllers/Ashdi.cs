using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using Lampac.Models.LITE.Ashdi;

namespace Lampac.Controllers.LITE
{
    public class Ashdi : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/ashdi")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t = -1, int s = -1)
        {
            var init = AppInit.conf.Ashdi;
            if (!init.enable || kinopoisk_id == 0)
                return OnError();

            var rch = new RchClient(HttpContext, host, init.rhub);
            var proxyManager = new ProxyManager("ashdi", init);
            var proxy = proxyManager.Get();

            var oninvk = new AshdiInvoke
            (
               host,
               init.corsHost(),
               ongettourl => init.rhub ? rch.Get(init.cors(ongettourl)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init), statusCodeOK: false),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "ashdi"),
               requesterror: () => proxyManager.Refresh()
            );

            var cache = await InvokeCache<EmbedModel>($"ashdi:view:{kinopoisk_id}", cacheTime(40, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(kinopoisk_id);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, kinopoisk_id, title, original_title, t, s));
        }
    }
}

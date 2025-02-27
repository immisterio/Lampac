using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Online;
using Shared.Engine.CORE;
using Shared.Model.Online.Kinobase;

namespace Lampac.Controllers.LITE
{
    public class Kinobase : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager(AppInit.conf.Kinobase);

        [HttpGet]
        [Route("lite/kinobase")]
        async public Task<ActionResult> Index(string title, int year, int s = -1)
        {
            var init = await loadKit(AppInit.conf.Kinobase);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(title) || year == 0)
                return OnError();

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            var proxy = proxyManager.Get();

            var oninvk = new KinobaseInvoke
            (
               host,
               init.corsHost(),
               ongettourl => rch.enable ? rch.Get(init.cors(ongettourl)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, referer: init.host, httpversion: 2, headers: httpHeaders(init)),
               (url, data) => rch.enable ? rch.Post(init.cors(url), data) : HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy),
               requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
            );

            var cache = await InvokeCache<EmbedModel>(rch.ipkey($"kinobase:view:{title}:{year}", proxyManager), cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(title, year);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, title, year, s));
        }
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;
using Shared.Engine;
using Shared.PlaywrightCore;

namespace Lampac.Controllers.Spankbang
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("sbg/vidosik")]
        async public Task<ActionResult> Index(string uri, bool related)
        {
            var init = await loadKit(AppInit.conf.Spankbang);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (PlaywrightBrowser.Status != PlaywrightStatus.NoHeadless)
                return OnError("NoHeadless");

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string memKey = $"spankbang:view:{uri}";
            if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await SpankbangTo.StreamLinks($"{host}/sbg/vidosik", init.corsHost(), uri, url =>
                {
                    return PlaywrightBrowser.Get(init, url, httpHeaders(init), proxy.data);
                });

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, stream_links, cacheTime(20, init: init));
            }

            if (related)
                return OnResult(stream_links?.recomends, null, plugin: init.plugin, total_pages: 1);

            return OnResult(stream_links, init, proxy.proxy);
        }
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;

namespace Lampac.Controllers.Xvideos
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("xds/vidosik")]
        async public Task<JsonResult> Index(string uri, bool related)
        {
            var init = AppInit.conf.Xvideos;

            if (!init.enable)
                return OnError("disable");

            var proxyManager = new ProxyManager("xds", init);
            var proxy = proxyManager.Get();

            string memKey = $"xvideos:view:{uri}";
            if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await XvideosTo.StreamLinks($"{host}/xds/vidosik", $"{host}/xds/stars", init.corsHost(), uri, url => HttpClient.Get(url, timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init)));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, stream_links, cacheTime(20, init: init));
            }

            if (related)
                return OnResult(stream_links?.recomends, null, plugin: "xds", total_pages: 1);

            return OnResult(stream_links, init, proxy, plugin: "xds");
        }
    }
}

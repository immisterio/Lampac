using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;

namespace Lampac.Controllers.XvideosRED
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("xdsred/vidosik")]
        async public Task<JsonResult> Index(string uri, bool related)
        {
            var init = AppInit.conf.XvideosRED;

            if (!init.enable)
                return OnError("disable");

            var proxyManager = new ProxyManager("xdsred", init);
            var proxy = proxyManager.Get();

            string memKey = $"xdsred:view:{uri}";
            if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await XvideosTo.StreamLinks($"{host}/xdsred/vidosik", $"{host}/xdsred/stars", init.corsHost(), uri, url => HttpClient.Get(url, cookie: init.cookie, timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init)));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, stream_links, cacheTime(20, init: init));
            }

            if (related)
                return OnResult(stream_links?.recomends, null, plugin: "xdsred", total_pages: 1);

            return OnResult(stream_links, init, proxy, plugin: "xdsred");
        }
    }
}

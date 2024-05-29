using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;

namespace Lampac.Controllers.Spankbang
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("sbg/vidosik")]
        async public Task<JsonResult> Index(string uri, bool related)
        {
            var init = AppInit.conf.Spankbang;

            if (!init.enable)
                return OnError("disable");

            string memKey = $"spankbang:view:{uri}";
            if (hybridCache.TryGetValue($"error:{memKey}", out string errormsg))
                return OnError(errormsg);

            var proxyManager = new ProxyManager("sbg", init);
            var proxy = proxyManager.Get();

            if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await SpankbangTo.StreamLinks($"{host}/sbg/vidosik", init.corsHost(), uri, 
                               url => HttpClient.Get(init.cors(url), httpversion: 2, timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init, ListController.headers)));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, stream_links, cacheTime(20, init: init));
            }

            if (related)
                return OnResult(stream_links?.recomends, null, plugin: "sbg", total_pages: 1);

            return OnResult(stream_links, init, proxy, plugin: "sbg");
        }
    }
}

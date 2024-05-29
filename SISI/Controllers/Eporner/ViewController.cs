using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;

namespace Lampac.Controllers.Eporner
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("epr/vidosik")]
        async public Task<JsonResult> Index(string uri, bool related)
        {
            var init = AppInit.conf.Eporner;

            if (!init.enable)
                return OnError("disable");

            var proxyManager = new ProxyManager("epr", init);
            var proxy = proxyManager.Get();

            string memKey = $"eporner:view:{uri}";
            if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await EpornerTo.StreamLinks($"{host}/epr/vidosik", init.corsHost(), uri, 
                               htmlurl => HttpClient.Get(init.cors(htmlurl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)), 
                               jsonurl => HttpClient.Get(init.cors(jsonurl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)));

                if (stream_links?.qualitys== null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, stream_links, cacheTime(20, init: init));
            }

            if (related)
                return OnResult(stream_links?.recomends, null, plugin: "epr", total_pages: 1);

            return OnResult(stream_links, init, proxy, plugin: "epr");
        }
    }
}

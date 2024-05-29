using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;

namespace Lampac.Controllers.PornHub
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("phub/vidosik")]
        async public Task<JsonResult> Index(string vkey, bool related)
        {
            var init = AppInit.conf.PornHub;

            if (!init.enable)
                return OnError("disable");

            string memKey = $"phub:vidosik:{vkey}";
            if (hybridCache.TryGetValue($"error:{memKey}", out string errormsg))
                return OnError(errormsg);

            var proxyManager = new ProxyManager("phub", init);
            var proxy = proxyManager.Get();

            if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await PornHubTo.StreamLinks($"{host}/phub/vidosik", "phub", init.corsHost(), vkey, url => HttpClient.Get(init.cors(url), httpversion: 2, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init, ListController.defaultheaders())));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, stream_links, cacheTime(20, init: init));
            }

            if (related)
                return OnResult(stream_links?.recomends, null, plugin: "phub", total_pages: 1);

            return OnResult(stream_links, init, proxy, plugin: "phub");
        }


        [HttpGet]
        [Route("phubprem/vidosik")]
        async public Task<JsonResult> Prem(string vkey, bool related)
        {
            var init = AppInit.conf.PornHubPremium;

            if (!init.enable)
                return OnError("disable");

            string memKey = $"phubprem:vidosik:{vkey}";
            if (hybridCache.TryGetValue($"error:{memKey}", out string errormsg))
                return OnError(errormsg);

            var proxyManager = new ProxyManager("phubprem", init);
            var proxy = proxyManager.Get();

            if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await PornHubTo.StreamLinks($"{host}/phubprem/vidosik", "phubprem", init.corsHost(), vkey, url => HttpClient.Get(init.cors(url), httpversion: 2, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init, ListController.defaultheaders(init.cookie))));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, stream_links, cacheTime(20, init: init));
            }

            if (related)
                return OnResult(stream_links?.recomends, null, plugin: "phubprem", total_pages: 1);

            return OnResult(stream_links, init, proxy, plugin: "phubprem");
        }
    }
}

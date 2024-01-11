using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
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
        async public Task<ActionResult> Index(string vkey)
        {
            var init = AppInit.conf.PornHub;

            if (!init.enable)
                return OnError("disable");

            string memKey = $"phub:vidosik:{vkey}";
            if (memoryCache.TryGetValue($"error:{memKey}", out string errormsg))
                return OnError(errormsg);

            var proxyManager = new ProxyManager("phub", init);
            var proxy = proxyManager.Get();

            if (!memoryCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await PornHubTo.StreamLinks($"{host}/phub/vidosik", init.corsHost(), vkey, url => HttpClient.Get(init.cors(url), httpversion: 2, timeoutSeconds: 8, proxy: proxy, addHeaders: ListController.httpheaders()));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                memoryCache.Set(memKey, stream_links, cacheTime(20));
            }

            return OnResult(stream_links, init, proxy, plugin: "phub");
        }


        [HttpGet]
        [Route("phubprem/vidosik")]
        async public Task<ActionResult> Prem(string vkey)
        {
            var init = AppInit.conf.PornHubPremium;

            if (!init.enable)
                return OnError("disable");

            string memKey = $"phubprem:vidosik:{vkey}";
            if (memoryCache.TryGetValue($"error:{memKey}", out string errormsg))
                return OnError(errormsg);

            var proxyManager = new ProxyManager("phubprem", init);
            var proxy = proxyManager.Get();

            if (!memoryCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await PornHubTo.StreamLinks($"{host}/phubprem/vidosik", init.corsHost(), vkey, url => HttpClient.Get(init.cors(url), httpversion: 2, timeoutSeconds: 8, proxy: proxy, addHeaders: ListController.httpheaders(init.cookie)));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                memoryCache.Set(memKey, stream_links, cacheTime(20));
            }

            return OnResult(stream_links, init, proxy, plugin: "phubprem");
        }
    }
}

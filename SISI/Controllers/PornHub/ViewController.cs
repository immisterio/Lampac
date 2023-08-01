using System;
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
            if (!AppInit.conf.PornHub.enable)
                return OnError("disable");

            string memKey = $"phub:vidosik:{vkey}";
            if (memoryCache.TryGetValue($"error:{memKey}", out string errormsg))
                return OnError(errormsg);

            var proxyManager = new ProxyManager("phub", AppInit.conf.PornHub);
            var proxy = proxyManager.Get();

            if (!memoryCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await PornHubTo.StreamLinks($"{host}/phub/vidosik", AppInit.conf.PornHub.host, vkey, url => HttpClient.Get(url, httpversion: 2, timeoutSeconds: 8, proxy: proxy, addHeaders: ListController.httpheaders()));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                memoryCache.Set(memKey, stream_links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            return OnResult(stream_links, AppInit.conf.PornHub, proxy);
        }


        [HttpGet]
        [Route("pornhubpremium/vidosik")]
        async public Task<ActionResult> Prem(string vkey)
        {
            if (!AppInit.conf.PornHubPremium.enable)
                return OnError("disable");

            string memKey = $"pornhubpremium:vidosik:{vkey}";
            if (memoryCache.TryGetValue($"error:{memKey}", out string errormsg))
                return OnError(errormsg);

            var proxyManager = new ProxyManager("pornhubpremium", AppInit.conf.PornHubPremium);
            var proxy = proxyManager.Get();

            if (!memoryCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await PornHubTo.StreamLinks($"{host}/pornhubpremium/vidosik", AppInit.conf.PornHubPremium.host, vkey, url => HttpClient.Get(url, httpversion: 2, timeoutSeconds: 8, proxy: proxy, addHeaders: ListController.httpheaders(AppInit.conf.PornHubPremium.cookie)));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                memoryCache.Set(memKey, stream_links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            return OnResult(stream_links, AppInit.conf.PornHubPremium, proxy);
        }
    }
}

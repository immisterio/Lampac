using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
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
        async public Task<ActionResult> Index(string uri)
        {
            if (!AppInit.conf.Spankbang.enable)
                return OnError("disable");

            string memKey = $"spankbang:view:{uri}";
            if (memoryCache.TryGetValue($"error:{memKey}", out string errormsg))
                return OnError(errormsg);

            var proxyManager = new ProxyManager("sbg", AppInit.conf.Spankbang);
            var proxy = proxyManager.Get();

            if (!memoryCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await SpankbangTo.StreamLinks($"{host}/sbg/vidosik", AppInit.conf.Spankbang.host, uri, 
                               url => HttpClient.Get(url, httpversion: 2, timeoutSeconds: 10, proxy: proxy, addHeaders: ListController.headers));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                memoryCache.Set(memKey, stream_links, cacheTime(20));
            }

            return OnResult(stream_links, AppInit.conf.Spankbang, proxy);
        }
    }
}

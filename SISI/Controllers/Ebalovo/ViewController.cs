using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared.Engine.SISI;
using System.Linq;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;

namespace Lampac.Controllers.Ebalovo
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("elo/vidosik")]
        async public Task<ActionResult> Index(string uri)
        {
            if (!AppInit.conf.Ebalovo.enable)
                return OnError("disable");

            var proxyManager = new ProxyManager("elo", AppInit.conf.Ebalovo);
            var proxy = proxyManager.Get();

            string memKey = $"ebalovo:view:{uri}";
            if (!memoryCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await EbalovoTo.StreamLinks($"{host}/elo/vidosik", AppInit.conf.Ebalovo.host, uri,
                               url => HttpClient.Get(url, timeoutSeconds: 8, proxy: proxy),
                               location => HttpClient.GetLocation(location, timeoutSeconds: 8, proxy: proxy, referer: $"{AppInit.conf.Ebalovo.host}/"));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                memoryCache.Set(memKey, stream_links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            return Json(new 
            {
                stream_links.recomends,
                qualitys = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(AppInit.conf.Ebalovo, v.Value, proxy: proxy))
            });
        }
    }
}

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
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
        async public Task<ActionResult> Index(string uri)
        {
            if (!AppInit.conf.Eporner.enable)
                return OnError("disable");

            var proxyManager = new ProxyManager("epr", AppInit.conf.Eporner);
            var proxy = proxyManager.Get();

            string memKey = $"eporner:view:{uri}";
            if (!memoryCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await EpornerTo.StreamLinks($"{host}/epr/vidosik", AppInit.conf.Eporner.host, uri, 
                               htmlurl => HttpClient.Get(htmlurl, timeoutSeconds: 8, proxy: proxy), 
                               jsonurl => HttpClient.Get(jsonurl, timeoutSeconds: 8, proxy: proxy));

                if (stream_links?.qualitys== null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                memoryCache.Set(memKey, stream_links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 2));
            }

            return OnResult(stream_links, AppInit.conf.Eporner, proxy);
        }
    }
}

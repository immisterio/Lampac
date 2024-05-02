using System.Collections.Generic;
using System.Threading.Tasks;
using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Engine.SISI;
using SISI;

namespace Lampac.Controllers.Chaturbate
{
    public class StreamController : BaseSisiController
    {
        [HttpGet]
        [Route("chu/potok")]
        async public Task<JsonResult> Index(string baba)
        {
            var init = AppInit.conf.Chaturbate;

            if (!init.enable)
                return OnError("disable");

            var proxyManager = new ProxyManager("chu", init);
            var proxy = proxyManager.Get();

            string memKey = $"chaturbate:stream:{baba}";
            if (!hybridCache.TryGetValue(memKey, out Dictionary<string, string> stream_links))
            {
                stream_links = await ChaturbateTo.StreamLinks(init.corsHost(), baba, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init)));
                if (stream_links == null || stream_links.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, stream_links, cacheTime(10, init: init));
            }

            return OnResult(stream_links, init, proxy);
        }
    }
}

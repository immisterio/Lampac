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
        async public Task<ActionResult> Index(string baba)
        {
            var init = AppInit.conf.Chaturbate.Clone();

            if (!init.enable)
                return OnError("disable");

            if (NoAccessGroup(init, out string error_msg))
                return OnError(error_msg, false);

            var proxyManager = new ProxyManager("chu", init);
            var proxy = proxyManager.Get();

            string memKey = $"chaturbate:stream:{baba}";
            if (!hybridCache.TryGetValue(memKey, out Dictionary<string, string> stream_links))
            {
                reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
                if (rch.IsNotSupport("web", out string rch_error))
                    return OnError(rch_error, false);

                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                stream_links = await ChaturbateTo.StreamLinks(init.corsHost(), baba, url =>
                    rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                );

                if (stream_links == null || stream_links.Count == 0)
                {
                    if (IsRhubFallback(init))
                        goto reset;

                    return OnError("stream_links", proxyManager, !init.rhub);
                }

                if (!init.rhub)
                    proxyManager.Success();

                hybridCache.Set(memKey, stream_links, cacheTime(10, init: init));
            }

            return OnResult(stream_links, init, proxy);
        }
    }
}

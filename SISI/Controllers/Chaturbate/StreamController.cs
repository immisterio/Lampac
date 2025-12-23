using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Chaturbate
{
    public class StreamController : BaseSisiController
    {
        [HttpGet]
        [Route("chu/potok")]
        async public ValueTask<ActionResult> Index(string baba)
        {
            var init = await loadKit(AppInit.conf.Chaturbate);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            return await SemaphoreResult($"chaturbate:stream:{baba}", async e =>
            {
                reset:
                if (rch.enable == false)
                    await e.semaphore.WaitAsync();

                if (!hybridCache.TryGetValue(e.key, out Dictionary<string, string> stream_links))
                {
                    stream_links = await ChaturbateTo.StreamLinks(init.corsHost(), baba, url =>
                        rch.enable
                            ? rch.Get(init.cors(url), httpHeaders(init))
                            : Http.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                    );

                    if (stream_links == null || stream_links.Count == 0)
                    {
                        if (IsRhubFallback(init))
                            goto reset;

                        return OnError("stream_links", proxyManager);
                    }

                    if (!init.rhub)
                        proxyManager.Success();

                    hybridCache.Set(e.key, stream_links, cacheTime(10, init: init));
                }

                return OnResult(stream_links, init, proxy);
            });
        }
    }
}

using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Ebalovo
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("elo/vidosik")]
        async public ValueTask<ActionResult> Index(string uri, bool related)
        {
            var init = await loadKit(AppInit.conf.Ebalovo);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return OnError(rch_error);

            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            if (rch.enable && 484 > rch.InfoConnected()?.apkVersion)
            {
                rch.Disabled(); // на версиях ниже java.lang.OutOfMemoryError
                if (!init.rhub_fallback)
                    return OnError("apkVersion", false);
            }

            string memKey = rch.ipkey($"ebalovo:view:{uri}", proxyManager);

            return await InvkSemaphore(memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
                {
                    string ehost = await RootController.goHost(init.corsHost());

                    reset:
                    stream_links = await EbalovoTo.StreamLinks("elo/vidosik", ehost, uri,
                        url =>
                        {
                            var headers = httpHeaders(init, HeadersModel.Init(
                                ("sec-fetch-dest", "document"),
                                ("sec-fetch-mode", "navigate"),
                                ("sec-fetch-site", "same-origin"),
                                ("sec-fetch-user", "?1"),
                                ("upgrade-insecure-requests", "1")
                            ));

                            return rch.enable ? rch.Get(init.cors(url), headers) : Http.Get(init.cors(url), timeoutSeconds: 8, proxy: proxy, headers: headers);
                        },
                        async location =>
                        {
                            var headers = httpHeaders(init, HeadersModel.Init(
                                ("referer", $"{ehost}/"),
                                ("sec-fetch-dest", "video"),
                                ("sec-fetch-mode", "no-cors"),
                                ("sec-fetch-site", "same-origin")
                            ));

                            if (rch.enable)
                            {
                                var res = await rch.Headers(init.cors(location), null, headers);
                                return res.currentUrl;
                            }

                            return await Http.GetLocation(init.cors(location), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                        }
                    );

                    if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    {
                        if (IsRhubFallback(init))
                            goto reset;

                        return OnError("stream_links", proxyManager);
                    }

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(memKey, stream_links, cacheTime(20, init: init));
                }

                if (related)
                    return OnResult(stream_links?.recomends, null, plugin: "elo", total_pages: 1);

                return OnResult(stream_links, init, proxy);
            });
        }
    }
}

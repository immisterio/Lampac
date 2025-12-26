using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Ebalovo
{
    public class ViewController : BaseSisiController
    {
        public ViewController() : base(AppInit.conf.Ebalovo) { }

        [HttpGet]
        [Route("elo/vidosik")]
        async public ValueTask<ActionResult> Index(string uri, bool related)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            if (rch.enable && 484 > rch.InfoConnected()?.apkVersion)
            {
                rch.Disabled(); // на версиях ниже java.lang.OutOfMemoryError
                if (!init.rhub_fallback)
                    return OnError("apkVersion", false);
            }

            rhubFallback:
            var cache = await InvokeCacheResult<StreamItem>(rch.ipkey($"ebalovo:view:{uri}", proxyManager), 20, async e =>
            {
                string ehost = await RootController.goHost(init.corsHost());

                var stream_links = await EbalovoTo.StreamLinks("elo/vidosik", ehost, uri,
                    url =>
                    {
                        return httpHydra.Get(url, addheaders: HeadersModel.Init(
                            ("sec-fetch-dest", "document"),
                            ("sec-fetch-mode", "navigate"),
                            ("sec-fetch-site", "same-origin"),
                            ("sec-fetch-user", "?1"),
                            ("upgrade-insecure-requests", "1")
                        ));
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
                    return e.Fail("stream_links", refresh_proxy: true);

                return e.Success(stream_links);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            if (related)
                return OnResult(cache.Value?.recomends, null, total_pages: 1);

            return OnResult(cache);
        }
    }
}

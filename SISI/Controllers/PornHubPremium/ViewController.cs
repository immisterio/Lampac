using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.PornHubPremium
{
    public class ViewController : BaseSisiController
    {
        public ViewController() : base(AppInit.conf.PornHubPremium) { }

        [HttpGet]
        [Route("phubprem/vidosik")]
        async public ValueTask<ActionResult> Prem(string vkey, bool related)
        {
            if (await IsRequestBlocked(rch: false))
                return badInitMsg;

            string memKey = $"phubprem:vidosik:{vkey}";
            if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await PornHubTo.StreamLinks("phubprem/vidosik", "phubprem", init.corsHost(), vkey, 
                    url => Http.Get(init.cors(url), httpversion: 2, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init, HeadersModel.Init("cookie", init.cookie)))
                );

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, stream_links, cacheTime(20));
            }

            if (related)
                return OnResult(stream_links?.recomends, null, total_pages: 1);

            return OnResult(stream_links);
        }
    }
}

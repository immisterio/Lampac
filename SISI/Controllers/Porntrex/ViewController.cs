using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Shared.Model.Online;

namespace Lampac.Controllers.Porntrex
{
    public class ViewController : BaseSisiController
    {
        ProxyManager proxyManager = new ProxyManager("ptx", AppInit.conf.Porntrex);

        [HttpGet]
        [Route("ptx/vidosik")]
        async public Task<ActionResult> vidosik(string uri)
        {
            var init = await loadKit(AppInit.conf.Porntrex);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return OnError(rch_error);

            string memKey = $"porntrex:view:{uri}";
            if (!hybridCache.TryGetValue(memKey, out Dictionary<string, string> links))
            {
                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                links = await PorntrexTo.StreamLinks(init.corsHost(), uri, url =>
                    rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxyManager.Get(), headers: httpHeaders(init))
                );

                if (links == null || links.Count == 0)
                {
                    if (IsRhubFallback(init))
                        goto reset;

                    return OnError("stream_links", proxyManager, !rch.enable);
                }

                if (!rch.enable)
                    proxyManager.Success();

                hybridCache.Set(memKey, links, cacheTime(20, init: init));
            }

            var hdstr = httpHeaders(init.host, HeadersModel.Init(init.headers_stream));
            return OnResult(links, init, proxyManager.Get(), headers_stream: hdstr);
        }
    }
}

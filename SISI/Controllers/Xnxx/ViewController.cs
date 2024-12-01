﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;

namespace Lampac.Controllers.Xnxx
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("xnx/vidosik")]
        async public Task<JsonResult> Index(string uri, bool related)
        {
            var init = AppInit.conf.Xnxx;

            if (!init.enable)
                return OnError("disable");

            if (NoAccessGroup(init, out string error_msg))
                return OnError(error_msg, false);

            var proxyManager = new ProxyManager("xnx", init);
            var proxy = proxyManager.Get();

            string memKey = $"xnxx:view:{uri}";
            if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await XnxxTo.StreamLinks($"{host}/xnx/vidosik", init.corsHost(), uri, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init)));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, stream_links, cacheTime(20, init: init));
            }

            if (related)
                return OnResult(stream_links?.recomends, null, plugin: "xnx", total_pages: 1);

            return OnResult(stream_links, init, proxy, plugin: "xnx");
        }
    }
}

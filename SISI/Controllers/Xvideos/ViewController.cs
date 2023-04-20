using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;

namespace Lampac.Controllers.Xvideos
{
    public class ViewController : BaseController
    {
        [HttpGet]
        [Route("xds/vidosik")]
        async public Task<ActionResult> Index(string uri)
        {
            if (!AppInit.conf.Xvideos.enable)
                return OnError("disable");

            string memKey = $"xvideos:view:{uri}";
            if (!memoryCache.TryGetValue(memKey, out Dictionary<string, string> stream_links))
            {
                stream_links = await XvideosTo.StreamLinks(AppInit.conf.Xvideos.host, uri, url => HttpClient.Get(url, timeoutSeconds: 10, useproxy: AppInit.conf.Xvideos.useproxy));

                if (stream_links == null || stream_links.Count == 0)
                    return OnError("stream_links");

                memoryCache.Set(memKey, stream_links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            return Json(stream_links.ToDictionary(k => k.Key, v => HostStreamProxy(AppInit.conf.Xvideos.streamproxy, v.Value)));
        }
    }
}

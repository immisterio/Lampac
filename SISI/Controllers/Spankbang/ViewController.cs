using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Linq;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;

namespace Lampac.Controllers.Spankbang
{
    public class ViewController : BaseController
    {
        [HttpGet]
        [Route("sbg/vidosik")]
        async public Task<ActionResult> Index(string uri)
        {
            if (!AppInit.conf.Spankbang.enable)
                return OnError("disable");

            string memKey = $"spankbang:view:{uri}";
            if (memoryCache.TryGetValue($"error:{memKey}", out string errormsg))
                return OnError(errormsg);

            if (!memoryCache.TryGetValue(memKey, out Dictionary<string, string> stream_links))
            {
                stream_links = await SpankbangTo.StreamLinks(AppInit.conf.Spankbang.host, uri, 
                               url => HttpClient.Get(url, httpversion: 2, timeoutSeconds: 10, useproxy: AppInit.conf.Spankbang.useproxy, addHeaders: ListController.headers));

                if (stream_links == null || stream_links.Count == 0)
                    return OnError("stream_links");

                memoryCache.Set(memKey, stream_links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            return Json(stream_links.ToDictionary(k => k.Key, v => HostStreamProxy(AppInit.conf.Spankbang.streamproxy, v.Value)));
        }
    }
}

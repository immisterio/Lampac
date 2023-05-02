using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;
using System.Linq;

namespace Lampac.Controllers.PornHub
{
    public class ViewController : BaseController
    {
        [HttpGet]
        [Route("phub/vidosik")]
        async public Task<ActionResult> Index(string vkey)
        {
            if (!AppInit.conf.PornHub.enable)
                return OnError("disable");

            string memKey = $"phub:vidosik:{vkey}";
            if (memoryCache.TryGetValue($"error:{memKey}", out string errormsg))
                return OnError(errormsg);

            if (!memoryCache.TryGetValue(memKey, out Dictionary<string, string> stream_links))
            {
                stream_links = await PornHubTo.StreamLinks(AppInit.conf.PornHub.host, vkey, url =>
                {
                    return HttpClient.Get(url, httpversion: 2, timeoutSeconds: 8, useproxy: AppInit.conf.PornHub.useproxy, addHeaders: new List<(string name, string val)>()
                    {
                        ("accept-language", "ru-RU,ru;q=0.9"),
                        ("sec-ch-ua", "\"Chromium\";v=\"94\", \"Google Chrome\";v=\"94\", \";Not A Brand\";v=\"99\""),
                        ("sec-ch-ua-mobile", "?0"),
                        ("sec-ch-ua-platform", "\"Windows\""),
                        ("sec-fetch-dest", "document"),
                        ("sec-fetch-mode", "navigate"),
                        ("sec-fetch-site", "none"),
                        ("sec-fetch-user", "?1"),
                        ("upgrade-insecure-requests", "1"),
                        ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/94.0.4606.81 Safari/537.36"),
                    });
                });

                if (stream_links == null || stream_links.Count == 0)
                    return OnError("stream_links");

                memoryCache.Set(memKey, stream_links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            return Json(stream_links.ToDictionary(k => k.Key, v => HostStreamProxy(AppInit.conf.PornHub.streamproxy, v.Value)));
        }
    }
}

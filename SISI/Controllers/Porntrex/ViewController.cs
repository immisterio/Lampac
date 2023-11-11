using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Web;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using System.Linq;

namespace Lampac.Controllers.Porntrex
{
    public class ViewController : BaseSisiController
    {
        ProxyManager proxyManager = new ProxyManager("ptx", AppInit.conf.Porntrex);

        [HttpGet]
        [Route("ptx/vidosik")]
        async public Task<ActionResult> vidosik(string uri)
        {
            if (!AppInit.conf.Porntrex.enable)
                return OnError("disable");

            string memKey = $"porntrex:view:{uri}:{proxyManager.CurrentProxyIp}";
            if (!memoryCache.TryGetValue(memKey, out Dictionary<string, string> links))
            {
                var proxy = proxyManager.Get();

                links = await PorntrexTo.StreamLinks(AppInit.conf.Porntrex.host, uri, url => HttpClient.Get(url, timeoutSeconds: 10, proxy: proxy));
                if (links == null || links.Count == 0)
                    return OnError("stream_links", proxyManager);

                memoryCache.Set(memKey, links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            return Json(links.ToDictionary(k => k.Key, v => $"{host}/ptx/strem?link={HttpUtility.UrlEncode(v.Value)}"));
        }


        [HttpGet]
        [Route("ptx/strem")]
        async public Task<ActionResult> strem(string link)
        {
            var proxy = proxyManager.Get();

            string memKey = $"Porntrex:strem:{link}";
            if (!memoryCache.TryGetValue(memKey, out string location))
            {
                location = await HttpClient.GetLocation(link, timeoutSeconds: 10, httpversion: 2, proxy: proxy, addHeaders: new List<(string name, string val)>() 
                {
                    ("sec-fetch-dest", "document"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "none"),
                    ("sec-fetch-user", "?1"),
                    ("upgrade-insecure-requests", "1"),
                    ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36")
                });

                if (location == null || link == location)
                    return OnError("location");

                memoryCache.Set(memKey, location, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 5));
            }

            return Redirect(HostStreamProxy(AppInit.conf.Porntrex, location, proxy: proxy));
        }
    }
}

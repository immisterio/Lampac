using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Web;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using System.Linq;
using Shared.Model.Online;

namespace Lampac.Controllers.Porntrex
{
    public class ViewController : BaseSisiController
    {
        ProxyManager proxyManager = new ProxyManager("ptx", AppInit.conf.Porntrex);

        [HttpGet]
        [Route("ptx/vidosik")]
        async public Task<JsonResult> vidosik(string uri)
        {
            var init = AppInit.conf.Porntrex;

            if (!init.enable)
                return OnError("disable");

            string memKey = $"porntrex:view:{uri}:{proxyManager.CurrentProxyIp}";
            if (!hybridCache.TryGetValue(memKey, out Dictionary<string, string> links))
            {
                var proxy = proxyManager.Get();

                links = await PorntrexTo.StreamLinks(init.corsHost(), uri, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init)));
                if (links == null || links.Count == 0)
                    return OnError("stream_links", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, links, cacheTime(20, init: init));
            }

            return Json(links.ToDictionary(k => k.Key, v => $"{host}/ptx/strem?link={HttpUtility.UrlEncode(v.Value)}"));
        }


        [HttpGet]
        [Route("ptx/strem")]
        async public Task<ActionResult> strem(string link)
        {
            var init = AppInit.conf.Porntrex;

            if (!init.enable)
                return OnError("disable");

            var proxy = proxyManager.Get();

            string memKey = $"Porntrex:strem:{link}";
            if (!hybridCache.TryGetValue(memKey, out string location))
            {
                location = await HttpClient.GetLocation(link, timeoutSeconds: 10, httpversion: 2, proxy: proxy, headers: httpHeaders(init, HeadersModel.Init(
                    ("sec-fetch-dest", "document"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "none"),
                    ("sec-fetch-user", "?1"),
                    ("upgrade-insecure-requests", "1"),
                    ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36")
                )));

                if (location == null || link == location)
                    return OnError("location");

                proxyManager.Success();
                hybridCache.Set(memKey, location, cacheTime(40, init: init));
            }

            return Redirect(HostStreamProxy(init, location, proxy: proxy));
        }
    }
}

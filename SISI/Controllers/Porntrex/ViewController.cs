using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Shared.Model.Online;
using System.Linq;
using System.Web;

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

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: init.apnstream ? -1 : null);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return OnError(rch_error);

            string memKey = rch.ipkey($"porntrex:view:{uri}", proxyManager);
            if (!hybridCache.TryGetValue(memKey, out (Dictionary<string, string> links, bool userch) cache))
            {
                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                cache.links = await PorntrexTo.StreamLinks(init.corsHost(), uri, url => 
                {
                    if (rch.enable)
                        return rch.Get(init.cors(url), httpHeaders(init));

                    return HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxyManager.Get(), headers: httpHeaders(init));
                });

                if (cache.links == null || cache.links.Count == 0)
                {
                    if (IsRhubFallback(init))
                        goto reset;

                    return OnError("stream_links", proxyManager);
                }

                if (!rch.enable)
                    proxyManager.Success();

                cache.userch = rch.enable;
                hybridCache.Set(memKey, cache, cacheTime(20, init: init));
            }

            if (cache.userch)
            {
                var hdstr = httpHeaders(init.host, HeadersModel.Init(init.headers_stream));
                return OnResult(cache.links, init, proxyManager.Get(), headers_stream: hdstr);
            }

            return Json(cache.links.ToDictionary(k => k.Key, v => $"{host}/ptx/strem?link={HttpUtility.UrlEncode(v.Value)}"));
        }


        [HttpGet]
        [Route("ptx/strem")]
        async public Task<ActionResult> strem(string link)
        {
            var init = await loadKit(AppInit.conf.Porntrex);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (init.rhub && !init.rhub_fallback)
                return OnError("rhub_fallback");

            var proxy = proxyManager.Get();

            string memKey = $"Porntrex:strem:{link}:{proxyManager.CurrentProxyIp}";
            if (!hybridCache.TryGetValue(memKey, out string location))
            {
                location = await HttpClient.GetLocation(link, timeoutSeconds: 10, httpversion: 2, proxy: proxy, headers: httpHeaders(init, HeadersModel.Init(
                    ("sec-fetch-dest", "document"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "none")
                )));

                if (string.IsNullOrEmpty(location) || link == location)
                    return OnError("location", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, location, cacheTime(40, init: init));
            }

            return Redirect(HostStreamProxy(init, location, proxy: proxy));
        }
    }
}

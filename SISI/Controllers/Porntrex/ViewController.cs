using Microsoft.AspNetCore.Mvc;
using System.Web;

namespace SISI.Controllers.Porntrex
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("ptx/vidosik")]
        async public ValueTask<ActionResult> vidosik(string uri)
        {
            var init = await loadKit(AppInit.conf.Porntrex);
            if (await IsBadInitialization(init, rch: true, rch_keepalive: -1))
                return badInitMsg;

            string semaphoreKey = $"porntrex:view:{uri}";

            return await InvkSemaphore(semaphoreKey, async () =>
            {
                reset:
                string memKey = rch.ipkey(semaphoreKey, proxyManager);
                if (!hybridCache.TryGetValue(memKey, out (Dictionary<string, string> links, bool userch) cache))
                {
                    cache.links = await PorntrexTo.StreamLinks(init.corsHost(), uri, url =>
                    {
                        if (rch.enable)
                            return rch.Get(init.cors(url), httpHeaders(init));

                        return Http.Get(init.cors(url), timeoutSeconds: 10, proxy: proxyManager.Get(), headers: httpHeaders(init));
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
                    var hdstr = httpHeaders(init.host, init.headers_stream);
                    return OnResult(cache.links, init, proxyManager.Get(), headers_stream: hdstr);
                }

                return Json(cache.links.ToDictionary(k => k.Key, v => $"{host}/ptx/strem?link={HttpUtility.UrlEncode(v.Value)}"));
            });
        }


        [HttpGet]
        [Route("ptx/strem")]
        async public ValueTask<ActionResult> strem(string link)
        {
            var init = await loadKit(AppInit.conf.Porntrex);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (rch.enable && 484 > rch.InfoConnected()?.apkVersion)
            {
                rch.Disabled(); // на версиях ниже java.lang.OutOfMemoryError
                if (!init.rhub_fallback)
                    return OnError("apkVersion", false);
            }

            string memKey = rch.ipkey($"Porntrex:strem:{link}", proxyManager);

            return await InvkSemaphore(memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out string location))
                {
                    var headers = httpHeaders(init, HeadersModel.Init(
                        ("sec-fetch-dest", "document"),
                        ("sec-fetch-mode", "navigate"),
                        ("sec-fetch-site", "none")
                    ));

                    if (rch.enable)
                    {
                        var res = await rch.Headers(init.cors(link), null, headers);
                        location = res.currentUrl;
                    }
                    else
                    {
                        location = await Http.GetLocation(init.cors(link), timeoutSeconds: 10, httpversion: 2, proxy: proxy, headers: headers);
                    }

                    if (string.IsNullOrEmpty(location) || link == location)
                        return OnError("location", proxyManager);

                    proxyManager.Success();
                    hybridCache.Set(memKey, location, cacheTime(40, init: init));
                }

                return Redirect(HostStreamProxy(init, location, proxy: proxy));
            });
        }
    }
}

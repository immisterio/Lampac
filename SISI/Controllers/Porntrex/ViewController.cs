using Microsoft.AspNetCore.Mvc;
using System.Web;

namespace SISI.Controllers.Porntrex
{
    public class ViewController : BaseSisiController
    {
        public ViewController() : base(AppInit.conf.Porntrex) { }

        [HttpGet]
        [Route("ptx/vidosik")]
        async public ValueTask<ActionResult> vidosik(string uri)
        {
            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            return await SemaphoreResult($"porntrex:view:{uri}", async e =>
            {
                reset:
                if (rch == null || rch.enable == false)
                    await e.semaphore.WaitAsync();

                string memKey = ipkey(e.key);
                if (!hybridCache.TryGetValue(memKey, out (Dictionary<string, string> links, bool userch) cache))
                {
                    string url = PorntrexTo.StreamLinksUri(init.corsHost(), uri);
                    if (url == null)
                        return OnError("uri");

                    await httpHydra.GetSpan(url, span => 
                    {
                        cache.links = PorntrexTo.StreamLinks(span);
                    });

                    if (cache.links == null || cache.links.Count == 0)
                    {
                        if (IsRhubFallback())
                            goto reset;

                        return OnError("stream_links", refresh_proxy: true);
                    }

                    proxyManager?.Success();

                    cache.userch = rch?.enable == true;
                    hybridCache.Set(memKey, cache, cacheTime(20));
                }

                if (cache.userch)
                    return OnResult(cache.links);

                return Json(cache.links.ToDictionary(k => k.Key, v => $"{host}/ptx/strem?link={HttpUtility.UrlEncode(v.Value)}"));
            });
        }


        [HttpGet]
        [Route("ptx/strem")]
        async public ValueTask<ActionResult> strem(string link)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            if (rch?.enable == true && 484 > rch.InfoConnected()?.apkVersion)
            {
                rch.Disabled(); // на версиях ниже java.lang.OutOfMemoryError
                if (!init.rhub_fallback)
                    return OnError("apkVersion", false);
            }

            return await SemaphoreResult($"Porntrex:strem:{link}", async e =>
            {
                if (rch == null || rch.enable == false)
                    await e.semaphore.WaitAsync();

                string memKey = ipkey(e.key);
                if (!hybridCache.TryGetValue(memKey, out string location))
                {
                    var headers = httpHeaders(init, HeadersModel.Init(
                        ("sec-fetch-dest", "document"),
                        ("sec-fetch-mode", "navigate"),
                        ("sec-fetch-site", "none")
                    ));

                    if (rch?.enable == true)
                    {
                        var res = await rch.Headers(init.cors(link), null, headers);
                        location = res.currentUrl;
                    }
                    else
                    {
                        location = await Http.GetLocation(init.cors(link), timeoutSeconds: init.httptimeout, httpversion: init.httpversion, proxy: proxy, headers: headers);
                    }

                    if (string.IsNullOrEmpty(location) || link == location)
                        return OnError("location", refresh_proxy: true);

                    proxyManager?.Success();
                    hybridCache.Set(memKey, location, cacheTime(40));
                }

                return Redirect(HostStreamProxy(location));
            });
        }
    }
}

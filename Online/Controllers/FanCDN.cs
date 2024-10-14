using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using Shared.Model.Online.VDBmovies;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using System.Collections.Generic;
using Shared.Model.Online;

namespace Lampac.Controllers.LITE
{
    public class FanCDN : BaseOnlineController
    {
        List<HeadersModel> baseheader = HeadersModel.Init(
            ("cache-control", "no-cache"),
            ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
            ("pragma", "no-cache"),
            ("priority", "u=0, i"),
            ("sec-ch-ua", "\"Google Chrome\";v=\"129\", \"Not = A ? Brand\";v=\"8\", \"Chromium\";v=\"129\""),
            ("sec-ch-ua-mobile", "?0"),
            ("sec-ch-ua-platform", "\"Windows\""),
            ("sec-fetch-dest", "document"),
            ("sec-fetch-mode", "navigate"),
            ("sec-fetch-site", "none"),
            ("sec-fetch-user", "?1"),
            ("upgrade-insecure-requests", "1")
        );


        [HttpGet]
        [Route("lite/fancdn")]
        async public Task<ActionResult> Index(string title, string original_title, int year, bool rjson = false)
        {
            var init = AppInit.conf.FanCDN.Clone();

#warning update online.js
            if (init.rhub)
                return OnError();

            if (!init.enable || string.IsNullOrEmpty(original_title) || year == 0)
                return OnError();

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            reset: var rch = new RchClient(HttpContext, host, init.rhub);
            var proxyManager = new ProxyManager("fancdn", init);
            var proxy = proxyManager.Get();

            var oninvk = new FanCDNInvoke
            (
               host,
               init.corsHost(),
               ongettourl => 
               {
                   var headers = httpHeaders(init, baseheader);
                   if (ongettourl.Contains("fancdn."))
                       headers.Add(new HeadersModel("referer", $"{init.host}/97258-deadpool-wolverine.html"));

                   return init.rhub ? rch.Get(init.cors(ongettourl)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: headers, httpversion: 2);
               },
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "fancdn")
            );

            var cache = await InvokeCache<List<Episode>>(rch.ipkey($"fancdn:{original_title}:{year}", proxyManager), cacheTime(20, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(title, original_title, year);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, title, original_title), rjson: rjson);
        }
    }
}

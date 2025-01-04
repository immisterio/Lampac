using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using Shared.Model.Online.VDBmovies;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using System.Collections.Generic;
using Shared.Model.Online;
using System.Net;
using System;

namespace Lampac.Controllers.LITE
{
    public class FanCDN : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/fancdn")]
        async public Task<ActionResult> Index(string title, string original_title, int year, bool origsource = false, bool rjson = false)
        {
            var init = AppInit.conf.FanCDN;

            if (!init.enable || string.IsNullOrEmpty(init.cookie) || string.IsNullOrEmpty(original_title) || year == 0)
                return OnError();

            if (init.rhub && !AppInit.conf.rch.enable)
                return ShowError(RchClient.ErrorMsg);

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            var proxyManager = new ProxyManager("fancdn", init);
            var proxy = proxyManager.Get();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            var oninvk = new FanCDNInvoke
            (
               host,
               init.corsHost(),
               ongettourl => 
               {
                   var headers = httpHeaders(init);
                   if (ongettourl.Contains("fancdn."))
                       headers.Add(new HeadersModel("referer", $"{init.host}/"));

                   if (rch.enable)
                       return rch.Get(init.cors(ongettourl), httpHeaders(init, HeadersModel.Init("cookie", init.cookie)));

                   return HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: headers, httpversion: 2, cookieContainer: cookieContainer(init.cookie));
               },
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "fancdn")
            );

            var cache = await InvokeCache<List<Episode>>($"fancdn:{original_title}:{year}", cacheTime(20, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(title, original_title, year);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, title, original_title, rjson: rjson), origsource: origsource);
        }


        #region cookieContainer
        static (string lastCook, CookieContainer cookies) container = default;

        static CookieContainer cookieContainer(string cook)
        {
            if (container.lastCook == cook)
                return container.cookies;

            container.lastCook = cook;
            container.cookies = new CookieContainer();

            foreach (string line in cook.Split(";"))
            {
                if (string.IsNullOrEmpty(line) || !line.Contains("="))
                    continue;

                string[] split = line.Split('=');
                container.cookies.Add(new Cookie()
                {
                    Path = "/",
                    Expires = DateTime.Now.AddHours(1),
                    Domain = ".fanserialstv.net",
                    Name = split[0].Trim(),
                    Value = split[1].Trim(),
                });
            }

            return container.cookies;
        }
        #endregion
    }
}

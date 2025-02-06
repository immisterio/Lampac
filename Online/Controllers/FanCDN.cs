using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using Shared.Model.Online.FanCDN;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Shared.Model.Online;
using System.Net;
using System;
using System.Text.RegularExpressions;

namespace Lampac.Controllers.LITE
{
    public class FanCDN : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/fancdn")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int year, int t = -1, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = AppInit.conf.FanCDN.Clone();

            if (!init.enable || (kinopoisk_id == 0 && (string.IsNullOrEmpty(title) || year == 0)))
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
                   var headers = rch.enable ? httpHeaders(init, HeadersModel.Init("cookie", init.cookie)) : httpHeaders(init);
                   if (ongettourl.Contains("fancdn."))
                       headers.Add(new HeadersModel("referer", $"{init.host}/"));

                   if (rch.enable)
                       return rch.Get(init.cors(ongettourl), headers);

                   return HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: headers, httpversion: 2, cookieContainer: cookieContainer(init.cookie));
               },
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "fancdn")
            );

            var cache = await InvokeCache<EmbedModel>($"fancdn:{title}:{year}:{kinopoisk_id}", cacheTime(20, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(kinopoisk_id, title, original_title, year);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, kinopoisk_id, title, original_title, t, s, rjson: rjson, vast: init.vast), origsource: origsource, gbcache: !rch.enable);
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
                    Expires = DateTime.Now.AddYears(1),
                    Domain = $".{Regex.Match(AppInit.conf.FanCDN.host, "https?://([^/]+)").Groups[1].Value}",
                    Name = split[0].Trim(),
                    Value = split[1].Trim(),
                });
            }

            return container.cookies;
        }
        #endregion
    }
}

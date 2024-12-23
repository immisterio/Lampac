using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Online.Eneyida;
using Shared.Model.Online;
using System;

namespace Lampac.Controllers.LITE
{
    public class Kinoukr : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinoukr")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, int t = -1, int s = -1, string href = null, bool origsource = false, bool rjson = false)
        {
            var init = AppInit.conf.Kinoukr.Clone();

            if (!init.enable)
                return OnError();

            if (init.rhub && !AppInit.conf.rch.enable)
                return ShowError(RchClient.ErrorMsg);

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(original_title) || year == 0))
                return OnError();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            var proxyManager = new ProxyManager("kinoukr", init);
            var proxy = proxyManager.Get();

            if (rch.IsNotSupport("web", out string rch_error))
                return ShowError(rch_error);

            var oninvk = new KinoukrInvoke
            (
               host,
               init.corsHost(),
               ongettourl => rch.enable ? rch.Get(init.cors(ongettourl), httpHeaders(init)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               (url, data) => rch.enable ? rch.Post(init.cors(url), data, httpHeaders(init)) : HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, headers: url.Contains("bobr-kurwa") ? httpHeaders(init) : httpHeaders(init, HeadersModel.Init
               (
                    ("cache-control", "no-cache"),
                    ("cookie", $"PHPSESSID={CrypTo.md5(DateTime.Now.ToBinary().ToString())}; legit_user=1;"),
                    ("dnt", "1"),
                    ("origin", init.host),
                    ("pragma", "no-cache"),
                    ("priority", "u=0, i"),
                    ("referer", $"{init.host}/{CrypTo.unic(4, true)}-{CrypTo.unic(Random.Shared.Next(4, 8))}-{CrypTo.unic(Random.Shared.Next(5, 10))}.html"),
                    ("sec-ch-ua", "\"Chromium\";v=\"130\", \"Google Chrome\";v=\"130\", \"Not ? A_Brand\";v=\"99\""),
                    ("sec-ch-ua-arch", "\"x86\""),
                    ("sec-ch-ua-bitness", "\"64\""),
                    ("sec-ch-ua-full-version", "\"130.0.6723.70\""),
                    ("sec-ch-ua-full-version-list", "\"Chromium\";v=\"130.0.6723.70\", \"Google Chrome\";v=\"130.0.6723.70\", \"Not ? A_Brand\";v=\"99.0.0.0\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-model", "\"\""),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-ch-ua-platform-version", "\"10.0.0\""),
                    ("sec-fetch-dest", "document"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "same-origin"),
                    ("sec-fetch-user", "?1"),
                    ("upgrade-insecure-requests", "1")
               ))),
               onstreamtofile => HostStreamProxy(init, onstreamtofile, proxy: proxy, plugin: "kinoukr"),
               requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
               //onlog: (l) => { Console.WriteLine(l); return string.Empty; }
            );

            var cache = await InvokeCache<EmbedModel>($"kinoukr:view:{title}:{year}:{href}:{clarification}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(clarification == 1 ? title : original_title, year, href);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, clarification, title, original_title, year, t, s, href, rjson: rjson), origsource: origsource);
        }
    }
}

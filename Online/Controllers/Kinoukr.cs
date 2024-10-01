﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Online.Eneyida;
using System;

namespace Lampac.Controllers.LITE
{
    public class Kinoukr : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinoukr")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, int t = -1, int s = -1, string href = null)
        {
            var init = AppInit.conf.Kinoukr.Clone();

            if (!init.enable)
                return OnError();

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(original_title) || year == 0))
                return OnError();

            reset: var rch = new RchClient(HttpContext, host, init.rhub);
            var proxyManager = new ProxyManager("kinoukr", init);
            var proxy = proxyManager.Get();

            var oninvk = new KinoukrInvoke
            (
               host,
               init.corsHost(),
               ongettourl => init.rhub ? rch.Get(init.cors(ongettourl)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               (url, data) => init.rhub ? rch.Post(init.cors(url), data) : HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               onstreamtofile => HostStreamProxy(init, onstreamtofile, proxy: proxy, plugin: "kinoukr"),
               requesterror: () => proxyManager.Refresh()
               //onlog: (l) => { Console.WriteLine(l); return string.Empty; }
            );

            var cache = await InvokeCache<EmbedModel>($"kinoukr:view:{title}:{year}:{href}:{clarification}", cacheTime(40, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(clarification == 1 ? title : original_title, year, href);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, clarification, title, original_title, year, t, s, href));
        }
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Models.LITE.AniLibria;
using Shared.Engine.CORE;
using Online;
using Shared.Engine.Online;

namespace Lampac.Controllers.LITE
{
    public class AniLibriaOnline : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/anilibria")]
        async public Task<ActionResult> Index(string title, string code, int year, bool origsource = false, bool rjson = false)
        {
            var init = AppInit.conf.AnilibriaOnline.Clone();
            if (!init.enable || string.IsNullOrEmpty(title))
                return OnError();

            if (init.rhub && !AppInit.conf.rch.enable)
                return ShowError(RchClient.ErrorMsg);

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            var proxyManager = new ProxyManager("anilibria", init);
            var proxy = proxyManager.Get();

            var oninvk = new AniLibriaInvoke
            (
               host,
               init.corsHost(),
               ongettourl => rch.enable ? rch.Get<List<RootObject>>(init.cors(ongettourl)) : HttpClient.Get<List<RootObject>>(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, IgnoreDeserializeObject: true, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "anilibria"),
               requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
            );

            var cache = await InvokeCache<List<RootObject>>($"anilibriaonline:{title}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(title);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, title, code, year, rjson: rjson), origsource: origsource);
        }
    }
}

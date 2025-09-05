using Microsoft.AspNetCore.Mvc;
using Shared.Models.Online.AniLibria;

namespace Online.Controllers
{
    public class AniLibriaOnline : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/anilibria")]
        async public ValueTask<ActionResult> Index(string title, string code, int year, bool origsource = false, bool rjson = false, bool similar = false)
        {
            var init = await loadKit(AppInit.conf.AnilibriaOnline);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(title))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var rch = new RchClient(HttpContext, host, init, requestInfo);

            var oninvk = new AniLibriaInvoke
            (
               host,
               init.corsHost(),
               ongettourl => rch.enable ? rch.Get<List<RootObject>>(init.cors(ongettourl)) : Http.Get<List<RootObject>>(init.cors(ongettourl), timeoutSeconds: 40, proxy: proxy, IgnoreDeserializeObject: true, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy),
               requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
            );

            reset:
            var cache = await InvokeCache<List<RootObject>>($"anilibriaonline:{title}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(title);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, title, code, year, vast: init.vast, rjson: rjson, similar: similar), origsource: origsource, gbcache: !rch.enable);
        }
    }
}

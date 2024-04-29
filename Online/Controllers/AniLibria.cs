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
        async public Task<ActionResult> Index(string title, string code, int year)
        {
            var init = AppInit.conf.AnilibriaOnline;

            if (!init.enable || string.IsNullOrWhiteSpace(title))
                return OnError();

            var rch = new RchClient(HttpContext, host, init.rhub);
            var proxyManager = new ProxyManager("anilibria", init);
            var proxy = proxyManager.Get();

            var oninvk = new AniLibriaInvoke
            (
               host,
               init.corsHost(),
               ongettourl => init.rhub ? rch.Get<List<RootObject>>(init.cors(ongettourl)) : HttpClient.Get<List<RootObject>>(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, IgnoreDeserializeObject: true, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "anilibria"),
               requesterror: () => proxyManager.Refresh()
            );

            var cache = await InvokeCache<List<RootObject>>($"anilibriaonline:{title}", cacheTime(40, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(title);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, title, code, year));
        }
    }
}

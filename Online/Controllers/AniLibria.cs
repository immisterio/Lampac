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

            var proxyManager = new ProxyManager("anilibria", init);
            var proxy = proxyManager.Get();

            var oninvk = new AniLibriaInvoke
            (
               host,
               init.corsHost(),
               ongettourl => HttpClient.Get<List<RootObject>>(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, IgnoreDeserializeObject: true),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "anilibria")
            );

            var result = await InvokeCache($"anilibriaonline:{title}", cacheTime(40), () => oninvk.Embed(title), proxyManager);
            if (result == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(result, title, code, year), "text/html; charset=utf-8");
        }
    }
}

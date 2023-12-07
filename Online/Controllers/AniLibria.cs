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
            if (!AppInit.conf.AnilibriaOnline.enable || string.IsNullOrWhiteSpace(title))
                return OnError();

            var proxyManager = new ProxyManager("anilibria", AppInit.conf.AnilibriaOnline);
            var proxy = proxyManager.Get();

            var oninvk = new AniLibriaInvoke
            (
               host,
               AppInit.conf.AnilibriaOnline.corsHost(),
               ongettourl => HttpClient.Get<List<RootObject>>(AppInit.conf.AnilibriaOnline.corsHost(ongettourl), timeoutSeconds: 8, proxy: proxy, IgnoreDeserializeObject: true),
               streamfile => HostStreamProxy(AppInit.conf.AnilibriaOnline, streamfile, proxy: proxy, plugin: "anilibria")
            );

            var result = await InvokeCache($"anilibriaonline:{title}", AppInit.conf.multiaccess ? 40 : 10, () => oninvk.Embed(title));
            if (result == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(result, title, code, year), "text/html; charset=utf-8");
        }
    }
}

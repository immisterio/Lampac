using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;

namespace Lampac.Controllers.LITE
{
    public class Redheadsound : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/redheadsound")]
        async public Task<ActionResult> Index(string title, string original_title, int year, int clarification, string original_language)
        {
            var init = AppInit.conf.Redheadsound;

            if (!init.enable)
                return OnError();

            if (original_language != "en")
                clarification = 1;

            if (string.IsNullOrWhiteSpace(title) || year == 0)
                return OnError();

            var proxyManager = new ProxyManager("redheadsound", init);
            var proxy = proxyManager.Get();

            var oninvk = new RedheadsoundInvoke
            (
               host,
               init.corsHost(),
               ongettourl => HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy),
               (url, data) => HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "redheadsound")
            );

            var content = await InvokeCache($"redheadsound:view:{title}:{year}:{clarification}", cacheTime(30), () => oninvk.Embed(clarification == 1 ? title : (original_title ?? title), year));
            if (content == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(content, title), "text/html; charset=utf-8");
        }
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Online.Collaps;

namespace Lampac.Controllers.LITE
{
    public class Collaps : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/collaps")]
        [Route("lite/collaps-dash")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int s = -1)
        {
            var init = AppInit.conf.Collaps.Clone();
            if (!init.enable)
                return OnError();

            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return OnError();

            string module = HttpContext.Request.Path.Value.StartsWith("/lite/collaps-dash") ? "dash" : "hls";
            if (module == "dash")
                init.dash = true;
            else if (init.two)
                init.dash = false;

            if (init.dash)
            {
                init.streamproxy = false;
                init.geostreamproxy = null;
            }

            var rch = new RchClient(HttpContext, host, init.rhub);
            var proxyManager = new ProxyManager("collaps", init);
            var proxy = proxyManager.Get();

            var oninvk = new CollapsInvoke
            (
               host,
               init.corsHost(),
               init.dash,
               ongettourl => init.rhub ? rch.Get(init.cors(ongettourl)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               onstreamtofile => init.rhub ? onstreamtofile : HostStreamProxy(init, onstreamtofile, proxy: proxy, plugin: "collaps"/*, headers: HeadersModel.Init("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1")*/),
               requesterror: () => proxyManager.Refresh()
            );

            var cache = await InvokeCache<EmbedModel>($"collaps-{module}:view:{imdb_id}:{kinopoisk_id}", cacheTime(20, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(imdb_id, kinopoisk_id);
            });

            return OnResult(cache, () => 
            {
                string html = oninvk.Html(cache.Value, imdb_id, kinopoisk_id, title, original_title, s);
                if (module == "dash")
                    html = html.Replace("lite/collaps", "lite/collaps-dash");

                return html;
            });
        }
    }
}

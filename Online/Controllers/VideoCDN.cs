using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Online.VideoCDN;

namespace Lampac.Controllers.LITE
{
    public class VideoCDN : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/vcdn")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, string t, int s = -1, int serial = -1)
        {
            var init = AppInit.conf.VCDN;
            if (!init.enable)
                return OnError();

            var rch = new RchClient(HttpContext, host, init.rhub);
            var proxyManager = new ProxyManager("vcdn", init);
            var proxy = proxyManager.Get();

            var oninvk = new VideoCDNInvoke
            (
               init,
               (url, referer) => init.rhub ? rch.Get(init.cors(url)) : HttpClient.Get(init.cors(url), referer: referer, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "vcdn"),
               host,
               requesterror: () => proxyManager.Refresh()
            );

            if (kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id))
            {
                return OnResult(await InvokeCache<string>($"videocdn:search:{title}:{original_title}", cacheTime(40, init: init), async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    return await oninvk.Search(title, original_title, serial);
                }));
            }

            var cache = await InvokeCache<EmbedModel>(rch.ipkey($"videocdn:{imdb_id}:{kinopoisk_id}", proxyManager), cacheTime(20, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(kinopoisk_id, imdb_id);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, imdb_id, kinopoisk_id, title, original_title, t, s));
        }
    }
}

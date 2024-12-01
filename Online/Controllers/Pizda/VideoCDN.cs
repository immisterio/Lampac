using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Online.VideoCDN;
using Shared.Model.Templates;

namespace Lampac.Controllers.LITE
{
    public class VideoCDN : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/vcdn")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, string t, int s = -1, int serial = -1, bool origsource = false, bool rjson = false)
        {
            var init = AppInit.conf.VCDN.Clone();
            if (!init.enable || init.rip)
                return OnError();

            if (init.rhub && !AppInit.conf.rch.enable)
                return ShowError(RchClient.ErrorMsg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            var proxyManager = new ProxyManager("vcdn", init);
            var proxy = proxyManager.Get();

            var oninvk = new VideoCDNInvoke
            (
               init,
               (url, referer) => rch.enable ? rch.Get(init.cors(url)) : HttpClient.Get(init.cors(url), referer: referer, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "vcdn"),
               host,
               requesterror: () => proxyManager.Refresh()
            );

            if (kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id))
            {
                var search = await InvokeCache<SimilarTpl>($"videocdn:search:{title}:{original_title}", cacheTime(40, init: init), async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    return await oninvk.Search(title, original_title, serial);
                });

                return OnResult(search, () => rjson ? search.Value.ToJson() : search.Value.ToHtml());
            }

            var cache = await InvokeCache<EmbedModel>(rch.ipkey($"videocdn:{imdb_id}:{kinopoisk_id}", proxyManager), cacheTime(20, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(kinopoisk_id, imdb_id);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, imdb_id, kinopoisk_id, title, original_title, t, s, rjson: rjson), origsource: origsource);
        }
    }
}

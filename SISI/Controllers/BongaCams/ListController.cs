using System.Collections.Generic;
using System.Threading.Tasks;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Engine.SISI;
using SISI;

namespace Lampac.Controllers.BongaCams
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("bgs")]
        async public Task<ActionResult> Index(string search, string sort, int pg = 1)
        {
            var init = AppInit.conf.BongaCams.Clone();

            if (!init.enable)
                return OnError("disable");

            if (NoAccessGroup(init, out string error_msg))
                return OnError(error_msg, false);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            if (!string.IsNullOrEmpty(search))
                return OnError("no search", false);

            var proxyManager = new ProxyManager("bgs", init);
            var proxy = proxyManager.Get();

            string memKey = $"BongaCams:list:{sort}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out (List<PlaylistItem> playlists, int total_pages) cache))
            {
                reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
                if (rch.IsNotSupport("web", out string rch_error))
                    return OnError(rch_error, false);

                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                string html = await BongaCamsTo.InvokeHtml(init.corsHost(), sort, pg, url => 
                {
                    if (rch.enable)
                        return rch.Get(init.cors(url), httpHeaders(init));

                    return HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, httpversion: 2, headers: httpHeaders(init));
                });

                cache.playlists = BongaCamsTo.Playlist(html, out int total_pages);
                cache.total_pages = total_pages;

                if (cache.playlists.Count == 0)
                {
                    if (IsRhubFallback(init))
                        goto reset;

                    return OnError("playlists", proxyManager, !rch.enable && pg > 1);
                }

                if (!rch.enable)
                    proxyManager.Success();

                hybridCache.Set(memKey, cache, cacheTime(5, init: init));
            }

            return OnResult(cache.playlists, init, BongaCamsTo.Menu(host, sort), proxy: proxy, plugin: "bgs", total_pages: cache.total_pages);
        }
    }
}

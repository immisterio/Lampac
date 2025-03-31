using System.Collections.Generic;
using System.Threading.Tasks;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using NetVips;
using Shared.Engine;
using Shared.Engine.CORE;
using Shared.Engine.SISI;
using Shared.PlaywrightCore;
using SISI;

namespace Lampac.Controllers.BongaCams
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("bgs")]
        async public Task<ActionResult> Index(string search, string sort, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.BongaCams);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (init.priorityBrowser != "http" && PlaywrightBrowser.Status != PlaywrightStatus.NoHeadless)
                return OnError("NoHeadless");

            if (!string.IsNullOrEmpty(search))
                return OnError("no search", false);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string memKey = $"BongaCams:list:{sort}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out (List<PlaylistItem> playlists, int total_pages) cache))
            {
                string html = await BongaCamsTo.InvokeHtml(init.corsHost(), sort, pg, url => 
                {
                    if (init.priorityBrowser == "http")
                        return HttpClient.Get(url, httpversion: 2, timeoutSeconds: 8, headers: httpHeaders(init), proxy: proxy.proxy);

                    return PlaywrightBrowser.Get(init, url, httpHeaders(init), proxy.data);
                });

                cache.playlists = BongaCamsTo.Playlist(html, out int total_pages);
                cache.total_pages = total_pages;

                if (cache.playlists.Count == 0)
                    return OnError("playlists", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, cache, cacheTime(5, init: init));
            }

            return OnResult(cache.playlists, init, BongaCamsTo.Menu(host, sort), proxy: proxy.proxy, total_pages: cache.total_pages);
        }



        [HttpGet]
        [Route("bgs2")]
        async public Task<ActionResult> Index2(string search, string sort, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.BongaCams);
            init.rhub = true;
            init.rhub_fallback = false;
            init.rhub_geo_disable = null;

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            if (!string.IsNullOrEmpty(search))
                return OnError("no search", false);

            string html = await BongaCamsTo.InvokeHtml(init.corsHost(), sort, pg, url =>
            {
                return rch.Get(url, headers: httpHeaders(init));
            });

            var playlists = BongaCamsTo.Playlist(html, out int total_pages);

            if ( playlists.Count == 0)
                return OnError("playlists");

            return OnResult(playlists, init, BongaCamsTo.Menu(host, sort), total_pages: total_pages);
        }
    }
}

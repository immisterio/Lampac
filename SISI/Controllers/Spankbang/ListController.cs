using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Models.SISI;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Shared.PlaywrightCore;
using Shared.Engine;
using Lampac.Engine.CORE;

namespace Lampac.Controllers.Spankbang
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("sbg")]
        async public Task<ActionResult> Index(string search, string sort, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.Spankbang);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (init.priorityBrowser != "http" && PlaywrightBrowser.Status != PlaywrightStatus.NoHeadless)
                return OnError("NoHeadless");

            string memKey = $"sbg:{search}:{sort}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager(init);
                var proxy = proxyManager.BaseGet();

                string html = await SpankbangTo.InvokeHtml(init.corsHost(), search, sort, pg, url =>
                {
                    if (init.priorityBrowser == "http")
                        return HttpClient.Get(url, httpversion: 2, timeoutSeconds: 8, headers: httpHeaders(init), proxy: proxy.proxy);

                    return PlaywrightBrowser.Get(init, url, httpHeaders(init), proxy.data);
                });

                playlists = SpankbangTo.Playlist($"{host}/sbg/vidosik", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));

                proxyManager.Success();
                hybridCache.Set(memKey, playlists, cacheTime(10, init: init));
            }

            return OnResult(playlists, string.IsNullOrEmpty(search) ? SpankbangTo.Menu(host, sort) : null, plugin: init.plugin);
        }
    }
}

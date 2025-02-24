using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Models.SISI;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;

namespace Lampac.Controllers.Spankbang
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("sbg")]
        async public Task<ActionResult> Index(string search, string sort, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.Spankbang);
            if (IsBadInitialization(init, out ActionResult action))
                return action;

            string memKey = $"sbg:{search}:{sort}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager(init);
                var proxy = proxyManager.Get();

                reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                string html = await SpankbangTo.InvokeHtml(init.corsHost(), search, sort, pg, url =>
                    rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : HttpClient.Get(init.cors(url), httpversion: 2, timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                );

                playlists = SpankbangTo.Playlist($"{host}/sbg/vidosik", html);

                if (playlists.Count == 0)
                {
                    if (IsRhubFallback(init))
                        goto reset;

                    return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));
                }

                if (!rch.enable)
                    proxyManager.Success();

                hybridCache.Set(memKey, playlists, cacheTime(10, init: init));
            }

            return OnResult(playlists, string.IsNullOrEmpty(search) ? SpankbangTo.Menu(host, sort) : null, plugin: init.plugin);
        }
    }
}

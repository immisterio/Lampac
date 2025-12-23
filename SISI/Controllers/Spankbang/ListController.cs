using Microsoft.AspNetCore.Mvc;
using Shared.PlaywrightCore;

namespace SISI.Controllers.Spankbang
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("sbg")]
        async public ValueTask<ActionResult> Index(string search, string sort, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.Spankbang);
            if (await IsBadInitialization(init, rch: true, rch_keepalive: -1))
                return badInitMsg;

            return await SemaphoreResult($"sbg:{search}:{sort}:{pg}", async e =>
            {
                reset:
                if (rch.enable == false)
                    await e.semaphore.WaitAsync();

                if (!hybridCache.TryGetValue(e.key, out List<PlaylistItem> playlists, inmemory: false))
                {
                    string html = await SpankbangTo.InvokeHtml(init.corsHost(), search, sort, pg, url =>
                    {
                        if (rch.enable)
                            return rch.Get(init.cors(url), httpHeaders(init));

                        if (init.priorityBrowser == "http")
                            return Http.Get(init.cors(url), httpversion: 2, timeoutSeconds: 8, headers: httpHeaders(init), proxy: proxy);

                        return PlaywrightBrowser.Get(init, init.cors(url), httpHeaders(init), proxy_data);
                    });

                    playlists = SpankbangTo.Playlist("sbg/vidosik", html);

                    if (playlists.Count == 0)
                    {
                        if (IsRhubFallback(init))
                            goto reset;

                        return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));
                    }

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(e.key, playlists, cacheTime(10, init: init), inmemory: false);
                }

                return OnResult(
                    playlists,
                    string.IsNullOrEmpty(search) ? SpankbangTo.Menu(host, sort) : null,
                    plugin: init.plugin,
                    imageHeaders: httpHeaders(init.host, init.headers_image)
                );
            });
        }
    }
}

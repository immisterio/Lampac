using Microsoft.AspNetCore.Mvc;
using Shared.PlaywrightCore;

namespace SISI.Controllers.Runetki
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("runetki")]
        async public ValueTask<ActionResult> Index(string search, string sort, int pg = 1)
        {
            if (!string.IsNullOrEmpty(search))
                return OnError("no search", false);

            var init = await loadKit(AppInit.conf.Runetki);
            if (await IsBadInitialization(init, rch: true, rch_keepalive: -1))
                return badInitMsg;

            return await SemaphoreResult($"{init.plugin}:list:{sort}:{pg}", async e =>
            {
                reset:
                if (rch.enable == false)
                    await e.semaphore.WaitAsync();

                if (!hybridCache.TryGetValue(e.key, out (List<PlaylistItem> playlists, int total_pages) cache, inmemory: false))
                {
                    string html = await RunetkiTo.InvokeHtml(init.corsHost(), sort, pg, url =>
                    {
                        if (rch.enable)
                            return rch.Get(init.cors(url), httpHeaders(init));

                        if (init.priorityBrowser == "http")
                            return Http.Get(init.cors(url), httpversion: 2, timeoutSeconds: 8, headers: httpHeaders(init), proxy: proxy);

                        return PlaywrightBrowser.Get(init, init.cors(url), httpHeaders(init), proxy_data);
                    });

                    cache.playlists = RunetkiTo.Playlist(html, out int total_pages);
                    cache.total_pages = total_pages;

                    if (cache.playlists.Count == 0)
                    {
                        if (IsRhubFallback(init))
                            goto reset;

                        return OnError("playlists", proxyManager);
                    }

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(e.key, cache, cacheTime(5, init: init), inmemory: false);
                }

                return OnResult(
                    cache.playlists,
                    init,
                    RunetkiTo.Menu(host, sort),
                    proxy: proxy,
                    total_pages: cache.total_pages
                );
            });
        }
    }
}

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
            var init = await loadKit(AppInit.conf.Runetki);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (!string.IsNullOrEmpty(search))
                return OnError("no search", false);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return OnError(rch_error);

            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            string memKey = $"{init.plugin}:list:{sort}:{pg}";

            return await InvkSemaphore(memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out (List<PlaylistItem> playlists, int total_pages) cache, inmemory: false))
                {
                    reset:
                    string html = await RunetkiTo.InvokeHtml(init.corsHost(), sort, pg, url =>
                    {
                        if (rch.enable)
                            return rch.Get(init.cors(url), httpHeaders(init));

                        if (init.priorityBrowser == "http")
                            return Http.Get(url, httpversion: 2, timeoutSeconds: 8, headers: httpHeaders(init), proxy: proxy.proxy);

                        return PlaywrightBrowser.Get(init, url, httpHeaders(init), proxy.data);
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

                    hybridCache.Set(memKey, cache, cacheTime(5, init: init), inmemory: false);
                }

                return OnResult(cache.playlists, init, RunetkiTo.Menu(host, sort), proxy: proxy.proxy, total_pages: cache.total_pages);
            });
        }
    }
}

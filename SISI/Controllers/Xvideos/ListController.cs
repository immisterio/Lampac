using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Xvideos
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("xds")]
        [Route("xdsgay")]
        [Route("xdssml")]
        async public ValueTask<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.Xvideos);
            if (await IsBadInitialization(init, rch: true, rch_keepalive: -1))
                return badInitMsg;

            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            return await SemaphoreResult($"{plugin}:list:{search}:{sort}:{c}:{pg}", async e =>
            {
                reset:
                if (rch.enable == false)
                    await e.semaphore.WaitAsync();

                if (!hybridCache.TryGetValue(e.key, out List<PlaylistItem> playlists, inmemory: false))
                {
                    string html = await XvideosTo.InvokeHtml(init.corsHost(), plugin, search, sort, c, pg, url =>
                        rch.enable
                            ? rch.Get(init.cors(url), httpHeaders(init))
                            : Http.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                    );

                    playlists = XvideosTo.Playlist("xds/vidosik", $"{plugin}/stars", html);

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
                    string.IsNullOrEmpty(search) ? XvideosTo.Menu(host, plugin, sort, c) : null,
                    plugin: init.plugin,
                    imageHeaders: httpHeaders(init.host, init.headers_image)
                );
            });
        }


        [HttpGet]
        [Route("xds/stars")]
        [Route("xdsgay/stars")]
        [Route("xdssml/stars")]
        async public ValueTask<ActionResult> Pornstars(string uri, string sort, int pg = 0)
        {
            var init = await loadKit(AppInit.conf.Xvideos);
            if (await IsBadInitialization(init, rch: true, rch_keepalive: -1))
                return badInitMsg;

            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            return await SemaphoreResult($"{plugin}:stars:{uri}:{sort}:{pg}", async e =>
            {
                reset:
                if (rch.enable == false)
                    await e.semaphore.WaitAsync();

                if (!hybridCache.TryGetValue(e.key, out List<PlaylistItem> playlists, inmemory: false))
                {
                    playlists = await XvideosTo.Pornstars("xds/vidosik", $"{plugin}/stars", init.corsHost(), plugin, uri, sort, pg, url =>
                        rch.enable
                            ? rch.Get(init.cors(url), httpHeaders(init))
                            : Http.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                    );

                    if (playlists == null || playlists.Count == 0)
                    {
                        if (IsRhubFallback(init))
                            goto reset;

                        return OnError("playlists", proxyManager);
                    }

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(e.key, playlists, cacheTime(10, init: init), inmemory: false);
                }

                return OnResult(
                    playlists,
                    null, // XvideosTo.PornstarsMenu(host, plugin, sort)
                    plugin: init.plugin,
                    imageHeaders: httpHeaders(init.host, init.headers_image)
                );
            });
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Shared.PlaywrightCore;

namespace SISI.Controllers.Runetki
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.Runetki) { }

        [HttpGet]
        [Route("runetki")]
        async public Task<ActionResult> Index(string search, string sort, int pg = 1)
        {
            if (!string.IsNullOrEmpty(search))
                return OnError("no search", false);

            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<(List<PlaylistItem> playlists, int total_pages)>($"{init.plugin}:list:{sort}:{pg}", 5, async e =>
            {
                string html = await RunetkiTo.InvokeHtml(init.corsHost(), sort, pg, url =>
                {
                    if (rch?.enable == true || init.priorityBrowser == "http")
                        return httpHydra.Get(url);

                    return PlaywrightBrowser.Get(init, init.cors(url), httpHeaders(init), proxy_data);
                });

                var playlists = RunetkiTo.Playlist(html, out int total_pages);

                if (playlists == null || playlists.Count == 0)
                    return e.Fail("playlists", refresh_proxy: true);

                return e.Success((playlists, total_pages));
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            return await PlaylistResult(
                cache.Value.playlists,
                RunetkiTo.Menu(host, sort),
                total_pages: cache.Value.total_pages
            );
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Shared.PlaywrightCore;

namespace SISI.Controllers.Spankbang
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.Spankbang) { }

        [HttpGet]
        [Route("sbg")]
        async public Task<ActionResult> Index(string search, string sort, int pg = 1)
        {
            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<List<PlaylistItem>>($"sbg:{search}:{sort}:{pg}", 10, async e =>
            {
                string html = await SpankbangTo.InvokeHtml(init.corsHost(), search, sort, pg, url =>
                {
                    if (rch?.enable == true || init.priorityBrowser == "http")
                        return httpHydra.Get(url);

                    return PlaywrightBrowser.Get(init, init.cors(url), httpHeaders(init), proxy_data);
                });

                var playlists = SpankbangTo.Playlist("sbg/vidosik", html);

                if (playlists == null || playlists.Count == 0)
                    return e.Fail("playlists", refresh_proxy: string.IsNullOrEmpty(search));

                return e.Success(playlists);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return await PlaylistResult(cache,
                string.IsNullOrEmpty(search) ? SpankbangTo.Menu(host, sort) : null
            );
        }
    }
}

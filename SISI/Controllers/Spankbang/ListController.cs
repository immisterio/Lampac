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
                string url = SpankbangTo.Uri(init.corsHost(), search, sort, pg);

                List<PlaylistItem> playlists = null;

                if (rch?.enable == true || init.priorityBrowser == "http")
                {
                    await httpHydra.GetSpan(url, span =>
                    {
                        playlists = SpankbangTo.Playlist("sbg/vidosik", span);
                    });
                }
                else
                {
                    string html = await PlaywrightBrowser.Get(init, url, httpHeaders(init), proxy_data);

                    playlists = SpankbangTo.Playlist("sbg/vidosik", html);
                }

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

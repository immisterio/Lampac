using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.HQporner
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.HQporner) { }

        [HttpGet]
        [Route("hqr")]
        async public Task<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<List<PlaylistItem>>($"hqr:{search}:{sort}:{c}:{pg}", 10, async e =>
            {
                string url = HQpornerTo.Uri(init.corsHost(), search, sort, c, pg);

                List<PlaylistItem> playlists = null;

                await httpHydra.GetSpan(url, span => 
                {
                    playlists = HQpornerTo.Playlist("hqr/vidosik", span);
                });

                if (playlists == null || playlists.Count == 0)
                    return e.Fail("playlists", refresh_proxy: string.IsNullOrEmpty(search));

                return e.Success(playlists);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return await PlaylistResult(cache,
                string.IsNullOrEmpty(search) ? HQpornerTo.Menu(host, sort, c) : null
            );
        }
    }
}

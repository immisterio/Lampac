using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Porntrex
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.Porntrex) { }

        [HttpGet]
        [Route("ptx")]
        async public Task<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<List<PlaylistItem>>($"ptx:{search}:{sort}:{c}:{pg}", 10, async e =>
            {
                string url = PorntrexTo.Uri(init.corsHost(), search, sort, c, pg);

                List<PlaylistItem> playlists = null;

                await httpHydra.GetSpan(url, span => 
                {
                    playlists = PorntrexTo.Playlist("ptx/vidosik", span);
                });

                if (playlists == null || playlists.Count == 0)
                    return e.Fail("playlists", refresh_proxy: string.IsNullOrEmpty(search));

                return e.Success(playlists);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return await PlaylistResult(cache,
                PorntrexTo.Menu(host, search, sort, c)
            );
        }
    }
}

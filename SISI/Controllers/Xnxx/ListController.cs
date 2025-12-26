using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Xnxx
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.Xnxx) { }

        [HttpGet]
        [Route("xnx")]
        async public ValueTask<ActionResult> Index(string search, int pg = 1)
        {
            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<List<PlaylistItem>>($"xnx:list:{search}:{pg}", 10, async e =>
            {
                string html = await XnxxTo.InvokeHtml(init.corsHost(), search, pg, 
                    url => httpHydra.Get(url)
                );

                var playlists = XnxxTo.Playlist("xnx/vidosik", html);

                if (playlists == null || playlists.Count == 0)
                    return e.Fail("playlists", refresh_proxy: string.IsNullOrEmpty(search));

                return e.Success(playlists);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return OnResult(cache, 
                string.IsNullOrEmpty(search) ? XnxxTo.Menu(host) : null
            );
        }
    }
}

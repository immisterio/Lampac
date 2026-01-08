using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Ebalovo
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.Ebalovo) { }

        [HttpGet]
        [Route("elo")]
        async public Task<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<List<PlaylistItem>>($"elo:{search}:{sort}:{c}:{pg}", 10, async e =>
            {
                string ehost = await RootController.goHost(init.corsHost(), proxy);

                string url = EbalovoTo.Uri(ehost, search, sort, c, pg);

                List<PlaylistItem> playlists = null;

                await httpHydra.GetSpan(url, span => 
                {
                    playlists = EbalovoTo.Playlist("elo/vidosik", span);
                }, 
                addheaders: HeadersModel.Init(
                    ("sec-fetch-dest", "document"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "same-origin"),
                    ("sec-fetch-user", "?1"),
                    ("upgrade-insecure-requests", "1")
                ));

                if (playlists == null || playlists.Count == 0)
                    return e.Fail("playlists", refresh_proxy: string.IsNullOrEmpty(search));

                return e.Success(playlists);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return await PlaylistResult(cache,
                string.IsNullOrEmpty(search) ? EbalovoTo.Menu(host, sort, c) : null
            );
        }
    }
}

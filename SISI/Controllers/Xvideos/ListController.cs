using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace SISI.Controllers.Xvideos
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.Xvideos) { }

        [HttpGet]
        [Route("xds")]
        [Route("xdsgay")]
        [Route("xdssml")]
        async public Task<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            rhubFallback:
            var cache = await InvokeCacheResult<List<PlaylistItem>>($"{plugin}:list:{search}:{sort}:{c}:{pg}", 10, async e =>
            {
                string url = XvideosTo.Uri(init.corsHost(), plugin, search, sort, c, pg);

                List<PlaylistItem> playlists = null;

                await httpHydra.GetSpan(url, span => 
                {
                    playlists = XvideosTo.Playlist("xds/vidosik", $"{plugin}/stars", span);
                });

                if (playlists == null || playlists.Count == 0)
                    return e.Fail("playlists", refresh_proxy: string.IsNullOrEmpty(search));

                return e.Success(playlists);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return await PlaylistResult(cache,
                string.IsNullOrEmpty(search) ? XvideosTo.Menu(host, plugin, sort, c) : null
            );
        }


        [HttpGet]
        [Route("xds/stars")]
        [Route("xdsgay/stars")]
        [Route("xdssml/stars")]
        async public Task<ActionResult> Pornstars(string uri, string sort, int pg = 0)
        {
            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            rhubFallback:
            var cache = await InvokeCacheResult<List<PlaylistItem>>($"{plugin}:stars:{uri}:{sort}:{pg}", 10, async e =>
            {
                var playlists = await XvideosTo.Pornstars("xds/vidosik", $"{plugin}/stars", init.corsHost(), plugin, uri, sort, pg, 
                    url => httpHydra.Get<JsonObject>(url)
                );

                if (playlists == null || playlists.Count == 0)
                    return e.Fail("playlists", refresh_proxy: true);

                return e.Success(playlists);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return await PlaylistResult(cache
                // XvideosTo.PornstarsMenu(host, plugin, sort)
            );
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.SISI.Base;
using Shared.PlaywrightCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Runetki;

public class RunetkiController : BaseSisiController
{
    public RunetkiController() : base(ModInit.conf) { }

    [HttpGet]
    [Staticache]
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
            string url = RunetkiTo.Uri(init.host, sort, pg);

            int total_pages = 1;
            List<PlaylistItem> playlists = null;

            if (rch?.enable == true || init.priorityBrowser == "http")
            {
                await httpHydra.GetSpan(url, span =>
                {
                    playlists = RunetkiTo.Playlist(span, out total_pages);
                });
            }
            else
            {
                var headers = httpHeaders(init);

                string html = await PlaywrightBrowser.Get(init, init.cors(url, headers, requestInfo), headers, proxy_data);

                playlists = RunetkiTo.Playlist(html, out total_pages);
            }

            if (playlists == null || playlists.Count == 0)
                return e.Fail("playlists", refresh_proxy: true);

            return e.Success((playlists, total_pages));
        });

        if (IsRhubFallback(cache))
            goto rhubFallback;

        if (!cache.IsSuccess)
            return OnError(cache.ErrorMsg);

        return PlaylistResult(
            cache.Value.playlists,
            cache.ISingleCache,
            RunetkiTo.Menu(host, sort),
            total_pages: cache.Value.total_pages
        );
    }
}

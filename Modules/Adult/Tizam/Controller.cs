using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.SISI.Base;
using Shared.Services;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Tizam;

public class TizamController : BaseSisiController
{
    static readonly HttpClient httpClient = FriendlyHttp.CreateHttpClient();

    public TizamController() : base(ModInit.conf)
    {
        requestInitialization += () =>
        {
            if (init.httpversion == 1)
                httpHydra.RegisterHttp(httpClient);
        };
    }

    [HttpGet]
    [Staticache]
    [Route("tizam")]
    async public Task<ActionResult> Index(string search, int pg = 1)
    {
        if (!string.IsNullOrEmpty(search))
            return OnError("no search", false);

        if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
            return badInitMsg;

    rhubFallback:
        var cache = await InvokeCacheResult($"tizam:{pg}", 60, jsonContext.ListPlaylistItem, async e =>
        {
            List<PlaylistItem> playlists = null;

            await httpHydra.GetSpan(TizamTo.Uri(init.host, pg), span =>
            {
                playlists = TizamTo.Playlist(init.host, span);
            });

            if (playlists == null || playlists.Count == 0)
                return e.Fail("playlists", refresh_proxy: true);

            return e.Success(playlists);
        });

        if (IsRhubFallback(cache))
            goto rhubFallback;

        return PlaylistResult(cache);
    }

    [HttpGet]
    [Staticache]
    [Route("tizam/vidosik")]
    async public Task<ActionResult> Index(string uri)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

    rhubFallback:
        var cache = await InvokeCacheResult($"tizam:view:{uri}", 180, jsonContext.StreamItem, async e =>
        {
            var stream = await TizamTo.Stream(httpHydra, init.host, uri);
            if (stream == null)
                return e.Fail("location", refresh_proxy: true);

            return e.Success(stream);
        });

        if (IsRhubFallback(cache))
            goto rhubFallback;

        return OnResult(cache);
    }
}

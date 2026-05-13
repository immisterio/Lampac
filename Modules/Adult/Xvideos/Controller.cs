using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.SISI.Base;
using Shared.Models.SISI.OnResult;
using Shared.Services;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Xvideos;

public class XvideosController : BaseSisiController
{
    static readonly HttpClient http2Client = FriendlyHttp.CreateHttp2Client();

    public XvideosController() : base(ModInit.conf)
    {
        requestInitialization += () =>
        {
            if (init.httpversion == 2)
                httpHydra.RegisterHttp(http2Client);
        };
    }

    [HttpGet]
    [Staticache]
    [Route("xds")]
    [Route("xdsgay")]
    [Route("xdssml")]
    async public Task<ActionResult> Index(string search, string sort, string c, int pg = 1)
    {
        if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
            return badInitMsg;

        string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

    rhubFallback:
        var cache = await InvokeCacheResult($"{plugin}:list:{search}:{sort}:{c}:{pg}", 10, jsonContext.ListPlaylistItem, async e =>
        {
            List<PlaylistItem> playlists = null;

            await httpHydra.GetSpan(XvideosTo.Uri(init.host, plugin, search, sort, c, pg), span =>
            {
                playlists = XvideosTo.Playlist("xds/vidosik", $"{plugin}/stars", span);
            });

            if (playlists == null || playlists.Count == 0)
                return e.Fail("playlists", refresh_proxy: string.IsNullOrEmpty(search));

            return e.Success(playlists);
        });

        if (IsRhubFallback(cache))
            goto rhubFallback;

        if (rch?.enable == true)
            StatiCacheDisabled = true;

        return PlaylistResult(cache,
            string.IsNullOrEmpty(search) ? XvideosTo.Menu(host, plugin, sort, c) : null
        );
    }

    [HttpGet]
    [Staticache]
    [Route("xds/stars")]
    [Route("xdsgay/stars")]
    [Route("xdssml/stars")]
    async public Task<ActionResult> Pornstars(string uri, string sort, int pg = 0)
    {
        if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
            return badInitMsg;

        if (pg != 0)
            pg = pg - 1;

        string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

    rhubFallback:
        var cache = await InvokeCacheResult($"{plugin}:stars:{uri}:{sort}:{pg}", 10, jsonContext.ListPlaylistItem, async e =>
        {
            var playlists = await XvideosTo.Pornstars("xds/vidosik", $"{plugin}/stars", init.host, plugin, uri, sort, pg,
                url => httpHydra.Get<PornstarsRoot>(url)
            );

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
    [Route("xds/vidosik")]
    async public Task<ActionResult> Index(string uri, bool related)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

    rhubFallback:
        var cache = await InvokeCacheResult($"xvideos:view:{uri}", 20, jsonContext.StreamItem, async e =>
        {
            string url = XvideosTo.StreamLinksUri($"{host}/xds/stars", init.host, uri);
            if (url == null)
                return e.Fail("uri");

            StreamItem stream_links = null;

            await httpHydra.GetSpan(url, span =>
            {
                stream_links = XvideosTo.StreamLinks(span, "xds/vidosik", $"{host}/xds/stars");
            });

            if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                return e.Fail("stream_links", refresh_proxy: true);

            return e.Success(stream_links);
        });

        if (IsRhubFallback(cache))
            goto rhubFallback;

        if (related)
            return PlaylistResult(cache.Value?.recomends, cache.ISingleCache, null, total_pages: 1);

        return OnResult(cache);
    }
}

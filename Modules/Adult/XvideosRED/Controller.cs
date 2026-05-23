using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace XvideosRED;

public class XvideosREDController : BaseSisiController
{
    public XvideosREDController() : base(ModInit.conf) { }

    [HttpGet, Staticache]
    [Route("xdsred")]
    async public Task<ActionResult> Index(string search, string sort, string c, int pg = 1)
    {
        if (await IsRequestBlocked(rch: false))
            return badInitMsg;

        string plugin = init.plugin;
        bool ismain = sort != "like" && string.IsNullOrEmpty(search) && string.IsNullOrEmpty(c);

        var cache = await InvokeCacheResult($"{plugin}:list:{search}:{c}:{sort}:{(ismain ? 0 : pg)}", 10, jsonContext.ListPlaylistItem, async e =>
        {
            string url;

            if (!string.IsNullOrEmpty(search))
            {
                url = $"{init.host}/?k={HttpUtility.UrlEncode(search)}&p={pg}&premium=1";
            }
            else
            {
                if (sort == "like")
                {
                    url = $"{init.host}/videos-i-like/{pg - 1}";
                }
                else if (!string.IsNullOrEmpty(c))
                {
                    url = $"{init.host}/c/s:{(sort == "top" ? "rating" : "uploaddate")}/p:1/{c}/{pg}";
                }
                else
                {
                    url = $"{init.host}/red/videos/{DateTime.Today.AddDays(-1):yyyy-MM-dd}";
                }
            }

            string html = await Http.Get(init.cors(url), cookie: init.cookie, timeoutSeconds: init.httptimeout, proxy: proxy, headers: httpHeaders(init));
            if (html == null)
                return e.Fail("html", refresh_proxy: string.IsNullOrEmpty(search));

            var playlists = XvideosTo.Playlist("xdsred/vidosik", $"{plugin}/stars", html, site: plugin);

            if (playlists.Count == 0)
                return e.Fail("playlists", refresh_proxy: pg > 1 && string.IsNullOrEmpty(search));

            return e.Success(playlists);
        });

        if (!cache.IsSuccess)
            return OnError(cache.ErrorMsg);

        var playlists = cache.Value;

        if (ismain)
            playlists = playlists.Skip((pg * 36) - 36).Take(36).ToList();

        return PlaylistResult(
            playlists,
            cache.ISingleCache,
            string.IsNullOrEmpty(search) ? XvideosTo.Menu(host, plugin, sort, c) : null
        );
    }

    [HttpGet, Staticache(manually: true)]
    [Route("xdsred/vidosik")]
    async public Task<ActionResult> Index(string uri, bool related)
    {
        if (await IsRequestBlocked(rch: false))
            return badInitMsg;

        var cache = await InvokeCacheResult($"xdsred:view:{uri}", 20, jsonContext.StreamItem, async e =>
        {
            string url = XvideosTo.StreamLinksUri("xdsred/stars", init.host, uri);
            if (url == null)
                return e.Fail("stream_links");

            string html = await Http.Get(url, cookie: init.cookie, timeoutSeconds: init.httptimeout, proxy: proxy, headers: httpHeaders(init));
            if (html == null)
                return e.Fail("stream_links");

            var stream_links = XvideosTo.StreamLinks(html, "xdsred/vidosik", "xdsred/stars");

            if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                return e.Fail("stream_links", refresh_proxy: true);

            return e.Success(stream_links);
        });

        if (!cache.IsSuccess)
            return OnError(cache.ErrorMsg);

        if (related)
            return PlaylistResult(cache.Value?.recomends, cache.ISingleCache, null, total_pages: 1);

        return OnResult(cache.Value);
    }
}

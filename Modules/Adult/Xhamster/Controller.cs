using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.SISI.Base;
using Shared.Models.SISI.OnResult;
using Shared.Services;
using Shared.Services.Hybrid;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Xhamster;

public class XhamsterController : BaseSisiController
{
    static readonly HttpClient http2Client = FriendlyHttp.CreateHttp2Client();

    public XhamsterController() : base(ModInit.conf)
    {
        requestInitialization += () =>
        {
            if (init.httpversion == 2)
                httpHydra.RegisterHttp(http2Client);
        };
    }

    [HttpGet]
    [Staticache(11)]
    [Route("xmr")]
    [Route("xmrgay")]
    [Route("xmrsml")]
    async public Task<ActionResult> Index(string search, string c, string q, string sort = "newest", int pg = 1)
    {
        if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
            return badInitMsg;

        pg++;
        string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

        SemaphorManager semaphore = null;
        string semaphoreKey = $"{plugin}:{search}:{sort}:{c}:{q}:{pg}";

        List<PlaylistItem> playlists = null;
        HybridCacheEntry<List<PlaylistItem>> entryCache;

        try
        {
        reset: // http запросы последовательно
            if (rch?.enable != true)
            {
                semaphore ??= new SemaphorManager(semaphoreKey, TimeSpan.FromSeconds(30));
                bool _acquired = await semaphore.WaitAsync();
                if (!_acquired)
                    return OnError();
            }

            entryCache = await hybridCache.EntryAsync(semaphoreKey, jsonType: jsonContext.ListPlaylistItem);

            // fallback cache
            if (!entryCache.success)
            {
                string userKey = headerKeys(semaphoreKey, "accept");

                bool next = rch == null;
                if (!next)
                {
                    // user cache разделенный по ip
                    entryCache = await hybridCache.EntryAsync(userKey, jsonType: jsonContext.ListPlaylistItem);
                    if (entryCache.success)
                        StatiCacheDisabled = true;
                    next = !entryCache.success;
                }

                if (next)
                {
                    await httpHydra.GetSpan(XhamsterTo.Uri(init.host, plugin, search, c, q, sort, pg), span =>
                    {
                        playlists = XhamsterTo.Playlist("xmr/vidosik", span);
                    });

                    if (playlists == null || playlists.Count == 0)
                    {
                        if (IsRhubFallback())
                            goto reset;

                        return OnError("playlists", refresh_proxy: string.IsNullOrEmpty(search));
                    }

                    string memKey = semaphoreKey;

                    if (rch?.enable == true)
                    {
                        memKey = userKey;
                        StatiCacheDisabled = true;
                    }
                    else
                    {
                        proxyManager?.Success();
                    }

                    hybridCache.Set(memKey, playlists, cacheTime(10));
                }
            }
        }
        finally
        {
            semaphore?.Release();
        }

        if (playlists == null)
            playlists = entryCache.value;

        return PlaylistResult(
            playlists,
            entryCache.singleCache,
            string.IsNullOrEmpty(search) ? XhamsterTo.Menu(host, plugin, c, q, sort) : null
        );
    }

    [HttpGet]
    [Staticache]
    [Route("xmr/vidosik")]
    async public Task<ActionResult> Index(string uri, bool related)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

    rhubFallback:
        var cache = await InvokeCacheResult($"xhamster:view:{uri}", 20, jsonContext.StreamItem, async e =>
        {
            string url = XhamsterTo.StreamLinksUri(init.host, uri);

            if (url == null)
                return e.Fail("uri");

            StreamItem stream_links = null;

            await httpHydra.GetSpan(url, span =>
            {
                stream_links = XhamsterTo.StreamLinks(init.host, "xmr/vidosik", span);
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

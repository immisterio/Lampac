using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.SISI.Base;
using Shared.Services;
using Shared.Services.Hybrid;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Eporner;

public class EpornerController : BaseSisiController
{
    static readonly HttpClient http2Client = FriendlyHttp.CreateHttp2Client();

    public EpornerController() : base(ModInit.conf)
    {
        requestInitialization += () =>
        {
            if (init.httpversion == 2)
                httpHydra.RegisterHttp(http2Client);
        };
    }

    [HttpGet]
    [Staticache(11)]
    [Route("epr")]
    async public Task<ActionResult> Index(string search, string sort, string c, int pg = 1)
    {
        if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
            return badInitMsg;

        pg += 1;

        SemaphorManager semaphore = null;
        string semaphoreKey = $"epr:{search}:{sort}:{c}:{pg}";

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
                    string url = EpornerTo.Uri(init.host, search, sort, c, pg);

                    await httpHydra.GetSpan(url, span =>
                    {
                        playlists = EpornerTo.Playlist("epr/vidosik", span);
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
            EpornerTo.Menu(host, search, sort, c)
        );
    }

    [HttpGet]
    [Route("epr/vidosik")]
    async public Task<ActionResult> Index(string uri, bool related)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

    rhubFallback:
        var cache = await InvokeCacheResult(ipkey($"eporner:view:{uri}"), 20, jsonContext.StreamItem, async e =>
        {
            var stream_links = await EpornerTo.StreamLinks(httpHydra, "epr/vidosik", init.host, uri);

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

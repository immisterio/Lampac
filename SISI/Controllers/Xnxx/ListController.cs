using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Xnxx
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("xnx")]
        async public ValueTask<ActionResult> Index(string search, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.Xnxx);
            if (await IsBadInitialization(init, rch: true, rch_keepalive: -1))
                return badInitMsg;

            return await SemaphoreResult($"xnx:list:{search}:{pg}", async e =>
            {
                reset:
                if (rch.enable == false)
                    await e.semaphore.WaitAsync();

                if (!hybridCache.TryGetValue(e.key, out List<PlaylistItem> playlists, inmemory: false))
                {
                    string html = await XnxxTo.InvokeHtml(init.corsHost(), search, pg, url =>
                        rch.enable
                            ? rch.Get(init.cors(url), httpHeaders(init))
                            : Http.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                    );

                    playlists = XnxxTo.Playlist("xnx/vidosik", html);

                    if (playlists.Count == 0)
                    {
                        if (IsRhubFallback(init))
                            goto reset;

                        return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));
                    }

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(e.key, playlists, cacheTime(10), inmemory: false);
                }

                return OnResult(
                    playlists,
                    string.IsNullOrEmpty(search) ? XnxxTo.Menu(host) : null,
                    plugin: init.plugin,
                    imageHeaders: httpHeaders(init.host, init.headers_image)
                );
            });
        }
    }
}

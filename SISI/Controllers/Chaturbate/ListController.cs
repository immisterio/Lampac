using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Chaturbate
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("chu")]
        async public ValueTask<ActionResult> Index(string search, string sort, int pg = 1)
        {
            if (!string.IsNullOrEmpty(search))
                return OnError("no search", false);

            var init = await loadKit(AppInit.conf.Chaturbate);
            if (await IsBadInitialization(init, rch: true, rch_keepalive: -1))
                return badInitMsg;

            return await SemaphoreResult($"Chaturbate:list:{sort}:{pg}", async e =>
            {
                reset:
                if (rch.enable == false)
                    await e.semaphore.WaitAsync();

                if (!hybridCache.TryGetValue(e.key, out List<PlaylistItem> playlists, inmemory: false))
                {
                    string html = await ChaturbateTo.InvokeHtml(init.corsHost(), sort, pg, url =>
                        rch.enable
                            ? rch.Get(init.cors(url), httpHeaders(init))
                            : Http.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                    );

                    playlists = ChaturbateTo.Playlist("chu/potok", html);

                    if (playlists.Count == 0)
                    {
                        if (IsRhubFallback(init))
                            goto reset;

                        return OnError("playlists", proxyManager);
                    }

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(e.key, playlists, cacheTime(5, init: init), inmemory: false);
                }

                return OnResult(
                    playlists,
                    ChaturbateTo.Menu(host, sort),
                    plugin: init.plugin,
                    imageHeaders: httpHeaders(init.host, init.headers_image)
                );
            });
        }
    }
}

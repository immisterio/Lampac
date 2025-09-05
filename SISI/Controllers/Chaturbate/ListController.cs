using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Chaturbate
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("chu")]
        async public ValueTask<ActionResult> Index(string search, string sort, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.Chaturbate);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (!string.IsNullOrEmpty(search))
                return OnError("no search", false);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotSupport("web", out string rch_error))
                return OnError(rch_error);

            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            string memKey = $"Chaturbate:list:{sort}:{pg}";

            return await InvkSemaphore(memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists, inmemory: false))
                {
                    reset: string html = await ChaturbateTo.InvokeHtml(init.corsHost(), sort, pg, url =>
                        rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : Http.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
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

                    hybridCache.Set(memKey, playlists, cacheTime(5, init: init), inmemory: false);
                }

                return OnResult(playlists, ChaturbateTo.Menu(host, sort), plugin: init.plugin);
            });
        }
    }
}

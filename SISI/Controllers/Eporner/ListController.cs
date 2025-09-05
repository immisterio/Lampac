using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Eporner
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("epr")]
        async public ValueTask<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.Eporner);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotSupport("web", out string rch_error))
                return OnError(rch_error);

            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            pg += 1;

            string memKey = $"epr:{search}:{sort}:{c}:{pg}";

            return await InvkSemaphore(memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists, inmemory: false))
                {
                    reset:
                    string html = await EpornerTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url =>
                        rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : Http.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                    );

                    playlists = EpornerTo.Playlist("epr/vidosik", html);

                    if (playlists.Count == 0)
                    {
                        if (IsRhubFallback(init))
                            goto reset;

                        return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));
                    }

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(memKey, playlists, cacheTime(10, init: init), inmemory: false);
                }

                return OnResult(playlists, EpornerTo.Menu(host, search, sort, c), plugin: init.plugin);
            });
        }
    }
}

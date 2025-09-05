using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Xhamster
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("xmr")]
        [Route("xmrgay")]
        [Route("xmrsml")]
        async public ValueTask<ActionResult> Index(string search, string c, string q, string sort = "newest", int pg = 1)
        {
            var init = await loadKit(AppInit.conf.Xhamster);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            pg++;
            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotSupport("web", out string rch_error))
                return OnError(rch_error);

            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            string memKey = $"{plugin}:{search}:{sort}:{c}:{q}:{pg}";

            return await InvkSemaphore(memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists, inmemory: false))
                {
                    reset:
                    string html = await XhamsterTo.InvokeHtml(init.corsHost(), plugin, search, c, q, sort, pg, url =>
                        rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : Http.Get(init.cors(url), httpversion: 2, timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                    );

                    playlists = XhamsterTo.Playlist("xmr/vidosik", html);

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

                return OnResult(playlists, string.IsNullOrEmpty(search) ? XhamsterTo.Menu(host, plugin, c, q, sort) : null, plugin: init.plugin);
            });
        }
    }
}

using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Ebalovo
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("elo")]
        async public ValueTask<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.Ebalovo);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotSupport("web", out string rch_error))
                return OnError(rch_error);

            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            string memKey = $"elo:{search}:{sort}:{c}:{pg}";

            return await InvkSemaphore(memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists, inmemory: false))
                {
                    var headers = httpHeaders(init, HeadersModel.Init(
                        ("sec-fetch-dest", "document"),
                        ("sec-fetch-mode", "navigate"),
                        ("sec-fetch-site", "same-origin"),
                        ("sec-fetch-user", "?1"),
                        ("upgrade-insecure-requests", "1")
                    ));

                    string ehost = await RootController.goHost(init.corsHost());

                    reset:
                    string html = await EbalovoTo.InvokeHtml(ehost, search, sort, c, pg, url =>
                        rch.enable ? rch.Get(init.cors(url), headers) : Http.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: headers)
                    );

                    playlists = EbalovoTo.Playlist("elo/vidosik", html);

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

                return OnResult(playlists, string.IsNullOrEmpty(search) ? EbalovoTo.Menu(host, sort, c) : null, plugin: init.plugin);
            });
        }
    }
}

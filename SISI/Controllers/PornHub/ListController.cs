using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.PornHub
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("phub")]
        [Route("phubgay")]
        [Route("phubsml")]
        async public ValueTask<ActionResult> Index(string search, string model, string sort, int c, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.PornHub);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotSupport("web", out string rch_error))
                return OnError(rch_error);

            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;
            string memKey = $"{plugin}:list:{search}:{model}:{sort}:{c}:{pg}";

            return await InvkSemaphore(memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out (int total_pages, List<PlaylistItem> playlists) cache, inmemory: false))
                {
                    reset:
                    string html = await PornHubTo.InvokeHtml(init.corsHost(), plugin, search, model, sort, c, null, pg, url =>
                        rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : Http.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, httpversion: 2, headers: httpHeaders(init))
                    );

                    cache.total_pages = rch.enable ? 0 : PornHubTo.Pages(html);
                    cache.playlists = PornHubTo.Playlist("phub/vidosik", "phub", html, IsModel_page: !string.IsNullOrEmpty(model));

                    if (cache.playlists.Count == 0)
                    {
                        if (IsRhubFallback(init))
                            goto reset;

                        return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));
                    }

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(memKey, cache, cacheTime(10, init: init), inmemory: false);
                }

                return OnResult(cache.playlists, string.IsNullOrEmpty(model) ? PornHubTo.Menu(host, plugin, search, sort, c) : null, plugin: init.plugin, total_pages: cache.total_pages);
            });
        }


        [HttpGet]
        [Route("phubprem")]
        async public ValueTask<ActionResult> Prem(string search, string model, string sort, string hd, int c, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.PornHubPremium);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            string memKey = $"phubprem:list:{search}:{model}:{sort}:{hd}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out (int total_pages, List<PlaylistItem> playlists) cache, inmemory: false))
            {
                var proxyManager = new ProxyManager(init);
                var proxy = proxyManager.Get();

                string html = await PornHubTo.InvokeHtml(init.corsHost(), "phubprem", search, model, sort, c, hd, pg, url => Http.Get(init.cors(url), timeoutSeconds: 14, proxy: proxy, httpversion: 2, headers: httpHeaders(init, HeadersModel.Init("cookie", init.cookie))));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                cache.total_pages = PornHubTo.Pages(html);
                cache.playlists = PornHubTo.Playlist("phubprem/vidosik", "phubprem", html, prem: true);

                if (cache.playlists.Count == 0)
                    return OnError("playlists", proxyManager, pg > 1 && string.IsNullOrEmpty(search));

                proxyManager.Success();
                hybridCache.Set(memKey, cache, cacheTime(10, init: init), inmemory: false);
            }

            return OnResult(cache.playlists, string.IsNullOrEmpty(model) ? PornHubTo.Menu(host, "phubprem", search, sort, c, hd) : null, plugin: "phubprem", total_pages: cache.total_pages);
        }
    }
}

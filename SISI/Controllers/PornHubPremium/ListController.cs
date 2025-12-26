using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.PornHubPremium
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.PornHubPremium) { }

        [HttpGet]
        [Route("phubprem")]
        async public ValueTask<ActionResult> Prem(string search, string model, string sort, string hd, int c, int pg = 1)
        {
            if (await IsRequestBlocked(rch: false))
                return badInitMsg;

            string memKey = $"phubprem:list:{search}:{model}:{sort}:{hd}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out (int total_pages, List<PlaylistItem> playlists) cache))
            {
                string html = await PornHubTo.InvokeHtml(init.corsHost(), "phubprem", search, model, sort, c, hd, pg, url => Http.Get(init.cors(url), timeoutSeconds: 14, proxy: proxy, httpversion: 2, headers: httpHeaders(init, HeadersModel.Init("cookie", init.cookie))));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                cache.total_pages = PornHubTo.Pages(html);
                cache.playlists = PornHubTo.Playlist("phubprem/vidosik", "phubprem", html, prem: true);

                if (cache.playlists.Count == 0)
                    return OnError("playlists", proxyManager, pg > 1 && string.IsNullOrEmpty(search));

                proxyManager.Success();
                hybridCache.Set(memKey, cache, cacheTime(10));
            }

            return OnResult(
                cache.playlists, 
                string.IsNullOrEmpty(model) ? PornHubTo.Menu(host, "phubprem", search, sort, c, hd) : null, 
                total_pages: cache.total_pages
            );
        }
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.PornHub
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.PornHub) { }

        [HttpGet]
        [Route("phub")]
        [Route("phubgay")]
        [Route("phubsml")]
        async public ValueTask<ActionResult> Index(string search, string model, string sort, int c, int pg = 1)
        {
            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            string semaphoreKey = $"{plugin}:list:{search}:{model}:{sort}:{c}:{pg}";
            var semaphore = new SemaphorManager(semaphoreKey, TimeSpan.FromSeconds(30));

            reset: // http запросы последовательно 
            if (rch.enable == false)
                await semaphore.WaitAsync();

            try
            {
                // fallback cache
                if (!hybridCache.TryGetValue(semaphoreKey, out (int total_pages, List<PlaylistItem> playlists) cache))
                {
                    // user cache разделенный по ip
                    if (rch.enable == false || !hybridCache.TryGetValue(rch.ipkey(semaphoreKey), out cache))
                    {
                        string html = await PornHubTo.InvokeHtml(init.corsHost(), plugin, search, model, sort, c, null, pg, 
                            url => httpHydra.Get(url)
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

                        hybridCache.Set(rch.ipkey(semaphoreKey), cache, cacheTime(10));
                    }
                }

                return OnResult(
                    cache.playlists,
                    string.IsNullOrEmpty(model) ? PornHubTo.Menu(host, plugin, search, sort, c) : null,
                    total_pages: cache.total_pages
                );
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}

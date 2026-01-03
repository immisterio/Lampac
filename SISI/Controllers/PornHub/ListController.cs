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
        async public Task<ActionResult> Index(string search, string model, string sort, int c, int pg = 1)
        {
            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            string semaphoreKey = $"{plugin}:list:{search}:{model}:{sort}:{c}:{pg}";
            var semaphore = new SemaphorManager(semaphoreKey, TimeSpan.FromSeconds(30));

            (int total_pages, List<PlaylistItem> playlists) cache;

            try
            {
                reset: // http запросы последовательно 
                if (rch?.enable != true)
                    await semaphore.WaitAsync();

                // fallback cache
                if (!hybridCache.TryGetValue(semaphoreKey, out cache))
                {
                    // user cache разделенный по ip
                    if (rch == null || !hybridCache.TryGetValue(ipkey(semaphoreKey, rch), out cache))
                    {
                        string html = await PornHubTo.InvokeHtml(init.corsHost(), plugin, search, model, sort, c, null, pg, 
                            url => httpHydra.Get(url)
                        );

                        cache.total_pages = PornHubTo.Pages(html);
                        cache.playlists = PornHubTo.Playlist("phub/vidosik", "phub", html, IsModel_page: !string.IsNullOrEmpty(model));

                        if (cache.playlists.Count == 0)
                        {
                            if (IsRhubFallback())
                                goto reset;

                            return OnError("playlists", refresh_proxy: string.IsNullOrEmpty(search));
                        }

                        proxyManager?.Success();

                        hybridCache.Set(ipkey(semaphoreKey, rch), cache, cacheTime(10));
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }

            return await PlaylistResult(
                cache.playlists,
                string.IsNullOrEmpty(model) ? PornHubTo.Menu(host, plugin, search, sort, c) : null,
                total_pages: cache.total_pages
            );
        }
    }
}

using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Xhamster
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.Xhamster) { }

        [HttpGet]
        [Route("xmr")]
        [Route("xmrgay")]
        [Route("xmrsml")]
        async public Task<ActionResult> Index(string search, string c, string q, string sort = "newest", int pg = 1)
        {
            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            pg++;
            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            string semaphoreKey = $"{plugin}:{search}:{sort}:{c}:{q}:{pg}";
            var semaphore = new SemaphorManager(semaphoreKey, TimeSpan.FromSeconds(30));

            List<PlaylistItem> playlists;

            try
            {
                reset: // http запросы последовательно 
                if (rch?.enable != true)
                    await semaphore.WaitAsync();

                // fallback cache
                if (!hybridCache.TryGetValue(semaphoreKey, out playlists))
                {
                    // user cache разделенный по ip
                    if (rch == null || !hybridCache.TryGetValue(ipkey(semaphoreKey, rch), out playlists))
                    {
                        string html = await XhamsterTo.InvokeHtml(init.corsHost(), plugin, search, c, q, sort, pg, 
                            url => httpHydra.Get(url)
                        );

                        playlists = XhamsterTo.Playlist("xmr/vidosik", html);

                        if (playlists.Count == 0)
                        {
                            if (IsRhubFallback())
                                goto reset;

                            return OnError("playlists", refresh_proxy: string.IsNullOrEmpty(search));
                        }

                        proxyManager?.Success();

                        hybridCache.Set(ipkey(semaphoreKey, rch), playlists, cacheTime(10));
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }

            return await PlaylistResult(
                playlists,
                string.IsNullOrEmpty(search) ? XhamsterTo.Menu(host, plugin, c, q, sort) : null
            );
        }
    }
}

using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Eporner
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.Eporner) { }

        [HttpGet]
        [Route("epr")]
        async public Task<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            pg += 1;

            string semaphoreKey = $"epr:{search}:{sort}:{c}:{pg}";
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
                        string html = await EpornerTo.InvokeHtml(init.corsHost(), search, sort, c, pg, 
                            url => httpHydra.Get(url)
                        );

                        playlists = EpornerTo.Playlist("epr/vidosik", html);

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
                EpornerTo.Menu(host, search, sort, c)
            );
        }
    }
}

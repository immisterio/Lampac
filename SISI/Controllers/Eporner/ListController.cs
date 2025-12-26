using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Eporner
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.Eporner) { }

        [HttpGet]
        [Route("epr")]
        async public ValueTask<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            pg += 1;

            string semaphoreKey = $"epr:{search}:{sort}:{c}:{pg}";
            var semaphore = new SemaphorManager(semaphoreKey, TimeSpan.FromSeconds(30));

            reset: // http запросы последовательно 
            if (rch.enable == false)
                await semaphore.WaitAsync();

            try
            {
                // fallback cache
                if (!hybridCache.TryGetValue(semaphoreKey, out List<PlaylistItem> playlists))
                {
                    // user cache разделенный по ip
                    if (rch.enable == false || !hybridCache.TryGetValue(rch.ipkey(semaphoreKey), out playlists))
                    {
                        string html = await EpornerTo.InvokeHtml(init.corsHost(), search, sort, c, pg, 
                            url => httpHydra.Get(url)
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

                        hybridCache.Set(rch.ipkey(semaphoreKey), playlists, cacheTime(10));
                    }
                }

                return OnResult(
                    playlists,
                    EpornerTo.Menu(host, search, sort, c)
                );
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}

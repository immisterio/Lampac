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
            if (await IsBadInitialization(init, rch: true, rch_keepalive: -1))
                return badInitMsg;

            pg++;
            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            string semaphoreKey = $"{plugin}:{search}:{sort}:{c}:{q}:{pg}";
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
                        string html = await XhamsterTo.InvokeHtml(init.corsHost(), plugin, search, c, q, sort, pg, url =>
                            rch.enable
                                ? rch.Get(init.cors(url), httpHeaders(init))
                                : Http.Get(init.cors(url), httpversion: 2, timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
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

                        hybridCache.Set(rch.ipkey(semaphoreKey), playlists, cacheTime(10, init: init));
                    }
                }

                return OnResult(
                    playlists,
                    string.IsNullOrEmpty(search) ? XhamsterTo.Menu(host, plugin, c, q, sort) : null,
                    plugin: init.plugin,
                    imageHeaders: httpHeaders(init.host, init.headers_image)
                );
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}

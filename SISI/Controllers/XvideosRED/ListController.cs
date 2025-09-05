using Microsoft.AspNetCore.Mvc;
using System.Web;

namespace SISI.Controllers.XvideosRED
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("xdsred")]
        async public ValueTask<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.XvideosRED);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            string plugin = init.plugin;
            bool ismain = sort != "like" && string.IsNullOrEmpty(search) && string.IsNullOrEmpty(c);
            string memKey = $"{plugin}:list:{search}:{c}:{sort}:{(ismain ? 0 : pg)}";

            return await InvkSemaphore(memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists, inmemory: false))
                {
                    var proxyManager = new ProxyManager(init);
                    var proxy = proxyManager.Get();

                    #region Генерируем url
                    string url;

                    if (!string.IsNullOrEmpty(search))
                    {
                        url = $"{init.corsHost()}/?k={HttpUtility.UrlEncode(search)}&p={pg}&premium=1";
                    }
                    else
                    {
                        if (sort == "like")
                        {
                            url = $"{init.corsHost()}/videos-i-like/{pg - 1}";
                        }
                        else if (!string.IsNullOrEmpty(c))
                        {
                            url = $"{init.corsHost()}/c/s:{(sort == "top" ? "rating" : "uploaddate")}/p:1/{c}/{pg}";
                        }
                        else
                        {
                            url = $"{init.corsHost()}/red/videos/{DateTime.Today.AddDays(-1):yyyy-MM-dd}";
                        }
                    }
                    #endregion

                    string html = await Http.Get(init.cors(url), cookie: init.cookie, timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init));
                    if (html == null)
                        return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                    playlists = XvideosTo.Playlist("xdsred/vidosik", $"{plugin}/stars", html, site: plugin);

                    if (playlists.Count == 0)
                        return OnError("playlists", proxyManager, pg > 1 && string.IsNullOrEmpty(search));

                    proxyManager.Success();
                    hybridCache.Set(memKey, playlists, cacheTime(10, init: init), inmemory: false);
                }

                if (ismain)
                    playlists = playlists.Skip((pg * 36) - 36).Take(36).ToList();

                return OnResult(playlists, string.IsNullOrEmpty(search) ? XvideosTo.Menu(host, plugin, sort, c) : null, plugin: plugin);
            });
        }
    }
}

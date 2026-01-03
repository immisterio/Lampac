using Microsoft.AspNetCore.Mvc;
using System.Web;

namespace SISI.Controllers.XvideosRED
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.XvideosRED) { }

        [HttpGet]
        [Route("xdsred")]
        async public Task<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            if (await IsRequestBlocked(rch: false))
                return badInitMsg;

            string plugin = init.plugin;
            bool ismain = sort != "like" && string.IsNullOrEmpty(search) && string.IsNullOrEmpty(c);

            return await InvkSemaphore($"{plugin}:list:{search}:{c}:{sort}:{(ismain ? 0 : pg)}", async key =>
            {
                if (!hybridCache.TryGetValue(key, out List<PlaylistItem> playlists, inmemory: false))
                {
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

                    string html = await Http.Get(init.cors(url), cookie: init.cookie, timeoutSeconds: init.httptimeout, proxy: proxy, headers: httpHeaders(init));
                    if (html == null)
                        return OnError("html", refresh_proxy: string.IsNullOrEmpty(search));

                    playlists = XvideosTo.Playlist("xdsred/vidosik", $"{plugin}/stars", html, site: plugin);

                    if (playlists.Count == 0)
                        return OnError("playlists", refresh_proxy: pg > 1 && string.IsNullOrEmpty(search));

                    proxyManager?.Success();
                    hybridCache.Set(key, playlists, cacheTime(10), inmemory: false);
                }

                if (ismain)
                    playlists = playlists.Skip((pg * 36) - 36).Take(36).ToList();

                return await PlaylistResult(
                    playlists,
                    string.IsNullOrEmpty(search) ? XvideosTo.Menu(host, plugin, sort, c) : null
                );
            });
        }
    }
}

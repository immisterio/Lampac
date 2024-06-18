using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using System.Web;
using System;
using System.Linq;

namespace Lampac.Controllers.XvideosRED
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("xdsred")]
        async public Task<JsonResult> Index(string search, string sort, string c, int pg = 1)
        {
            var init = AppInit.conf.XvideosRED;

            if (!init.enable)
                return OnError("disable");

            string plugin = "xdsred";
            bool ismain = sort != "like" && string.IsNullOrEmpty(search) && string.IsNullOrEmpty(c);
            string memKey = $"{plugin}:list:{search}:{c}:{sort}:{(ismain ? 0 : pg)}";

            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager(plugin, init);
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
                        url = $"{init.corsHost()}/videos-i-like/{pg-1}";
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

                string html = await HttpClient.Get(init.cors(url), cookie: init.cookie, timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                playlists = XvideosTo.Playlist($"{host}/xdsred/vidosik", $"{plugin}/stars", html, site: plugin);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager, pg > 1 && string.IsNullOrEmpty(search));

                proxyManager.Success();
                hybridCache.Set(memKey, playlists, cacheTime(10, init: init));
            }

            if (ismain)
                playlists = playlists.Skip((pg * 36) - 36).Take(36).ToList();

            return OnResult(playlists, string.IsNullOrEmpty(search) ? XvideosTo.Menu(host, plugin, sort, c) : null, plugin: plugin);
        }
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using System.Text.RegularExpressions;

namespace Lampac.Controllers.Xvideos
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("xds")]
        [Route("xdsgay")]
        [Route("xdssml")]
        async public Task<JsonResult> Index(string search, string sort, string c, int pg = 1)
        {
            var init = AppInit.conf.Xvideos;

            if (!init.enable)
                return OnError("disable");

            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            string memKey = $"{plugin}:list:{search}:{sort}:{c}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("xds", init);
                var proxy = proxyManager.Get();

                string html = await XvideosTo.InvokeHtml(init.corsHost(), plugin, search, sort, c, pg, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init)));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                playlists = XvideosTo.Playlist($"{host}/xds/vidosik", $"{plugin}/stars", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager, pg > 1 && string.IsNullOrEmpty(search));

                proxyManager.Success();
                hybridCache.Set(memKey, playlists, cacheTime(10, init: init));
            }

            return OnResult(playlists, string.IsNullOrEmpty(search) ? XvideosTo.Menu(host, plugin, sort, c) : null, plugin: "xds");
        }


        [HttpGet]
        [Route("xds/stars")]
        [Route("xdsgay/stars")]
        [Route("xdssml/stars")]
        async public Task<JsonResult> Pornstars(string uri, string sort, int pg = 0)
        {
            var init = AppInit.conf.Xvideos;

            if (!init.enable)
                return OnError("disable");

            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            string memKey = $"{plugin}:stars:{uri}:{sort}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("xds", init);
                var proxy = proxyManager.Get();

                playlists = await XvideosTo.Pornstars($"{host}/xds/vidosik", $"{plugin}/stars", init.corsHost(), plugin, uri, sort, pg, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init)));

                if (playlists == null || playlists.Count == 0)
                    return OnError("playlists", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, playlists, cacheTime(10, init: init));
            }


            // XvideosTo.PornstarsMenu(host, plugin, sort)
            return OnResult(playlists, null, plugin: "xds");
        }
    }
}

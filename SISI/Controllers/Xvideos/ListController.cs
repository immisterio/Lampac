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
        async public Task<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            if (IsBadInitialization(AppInit.conf.Xvideos, out ActionResult action))
                return action;

            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            string memKey = $"{plugin}:list:{search}:{sort}:{c}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("xds", init);
                var proxy = proxyManager.Get();

                reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
                if (rch.IsNotSupport("web", out string rch_error))
                    return OnError(rch_error);

                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                string html = await XvideosTo.InvokeHtml(init.corsHost(), plugin, search, sort, c, pg, url =>
                    rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                );

                playlists = XvideosTo.Playlist($"{host}/xds/vidosik", $"{plugin}/stars", html);

                if (playlists.Count == 0)
                {
                    if (IsRhubFallback(init))
                        goto reset;

                    return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));
                }

                if (!rch.enable)
                    proxyManager.Success();

                hybridCache.Set(memKey, playlists, cacheTime(10, init: init));
            }

            return OnResult(playlists, string.IsNullOrEmpty(search) ? XvideosTo.Menu(host, plugin, sort, c) : null, plugin: "xds");
        }


        [HttpGet]
        [Route("xds/stars")]
        [Route("xdsgay/stars")]
        [Route("xdssml/stars")]
        async public Task<ActionResult> Pornstars(string uri, string sort, int pg = 0)
        {
            if (IsBadInitialization(AppInit.conf.Xvideos, out ActionResult action))
                return action;

            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            string memKey = $"{plugin}:stars:{uri}:{sort}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("xds", init);
                var proxy = proxyManager.Get();

                reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
                if (rch.IsNotSupport("web", out string rch_error))
                    return OnError(rch_error);

                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                playlists = await XvideosTo.Pornstars($"{host}/xds/vidosik", $"{plugin}/stars", init.corsHost(), plugin, uri, sort, pg, url =>
                    rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                );

                if (playlists == null || playlists.Count == 0)
                {
                    if (IsRhubFallback(init))
                        goto reset;

                    return OnError("playlists", proxyManager);
                }

                if (!rch.enable)
                    proxyManager.Success();

                hybridCache.Set(memKey, playlists, cacheTime(10, init: init));
            }


            // XvideosTo.PornstarsMenu(host, plugin, sort)
            return OnResult(playlists, null, plugin: "xds");
        }
    }
}

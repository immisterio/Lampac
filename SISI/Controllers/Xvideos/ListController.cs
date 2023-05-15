using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared.Engine.SISI;
using Shared.Engine.CORE;

namespace Lampac.Controllers.Xvideos
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("xds")]
        async public Task<JsonResult> Index(string search, int pg = 1)
        {
            if (!AppInit.conf.Xvideos.enable)
                return OnError("disable");

            string memKey = $"xds:list:{search}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("xds", AppInit.conf.Xvideos);
                var proxy = proxyManager.Get();

                string html = await XvideosTo.InvokeHtml(AppInit.conf.Xvideos.host, search, pg, url => HttpClient.Get(url, timeoutSeconds: 10, proxy: proxy));
                if (html == null)
                {
                    proxyManager.Refresh();
                    return OnError("html");
                }

                playlists = XvideosTo.Playlist($"{host}/xds/vidosik", html, pl =>
                {
                    pl.picture = HostImgProxy(0, AppInit.conf.sisi.heightPicture, pl.picture);
                    return pl;
                });

                if (playlists.Count == 0)
                {
                    proxyManager.Refresh();
                    return OnError("playlists");
                }

                memoryCache.Set(memKey, playlists, TimeSpan.FromMinutes(AppInit.conf.multiaccess ? 10 : 2));
            }

            return new JsonResult(new
            {
                menu = XvideosTo.Menu(host),
                list = playlists
            });
        }
    }
}

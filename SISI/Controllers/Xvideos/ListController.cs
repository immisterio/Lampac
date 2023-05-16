using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;

namespace Lampac.Controllers.Xvideos
{
    public class ListController : BaseSisiController
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
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                playlists = XvideosTo.Playlist($"{host}/xds/vidosik", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));

                memoryCache.Set(memKey, playlists, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 10 : 2));
            }

            return OnResult(playlists, XvideosTo.Menu(host));
        }
    }
}

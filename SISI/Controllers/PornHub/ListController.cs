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

namespace Lampac.Controllers.PornHub
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("phub")]
        async public Task<JsonResult> Index(string search, string sort, int pg = 1)
        {
            if (!AppInit.conf.PornHub.enable)
                return OnError("disable");

            string memKey = $"PornHub:list:{search}:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("phub", AppInit.conf.PornHub);
                var proxy = proxyManager.Get();

                string html = await PornHubTo.InvokeHtml(AppInit.conf.PornHub.host, search, sort, pg, url => HttpClient.Get(url, timeoutSeconds: 10, proxy: proxy));
                if (html == null)
                {
                    proxyManager.Refresh();
                    return OnError("html");
                }

                playlists = PornHubTo.Playlist($"{host}/phub/vidosik", html, pl =>
                {
                    pl.picture = HostImgProxy(0, AppInit.conf.sisi.heightPicture, pl.picture);
                    return pl;
                });

                if (playlists.Count == 0)
                {
                    proxyManager.Refresh();
                    return OnError("playlists");
                }

                memoryCache.Set(memKey, playlists, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 10 : 2));
            }

            return new JsonResult(new
            {
                menu = PornHubTo.Menu(host, sort),
                list = playlists
            });
        }
    }
}

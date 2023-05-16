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

namespace Lampac.Controllers.Xhamster
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("xmr")]
        async public Task<JsonResult> Index(string search, string sort = "newest", int pg = 1)
        {
            if (!AppInit.conf.Xhamster.enable)
                return OnError("disable");

            pg++;
            string memKey = $"xmr:{search}:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("xmr", AppInit.conf.Xhamster);
                var proxy = proxyManager.Get();

                string html = await XhamsterTo.InvokeHtml(AppInit.conf.Xhamster.host, search, sort, pg, url => HttpClient.Get(url, timeoutSeconds: 10, proxy: proxy));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                playlists = XhamsterTo.Playlist($"{host}/xmr/vidosik", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));

                memoryCache.Set(memKey, playlists, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 10 : 2));
            }

            return OnResult(playlists, XhamsterTo.Menu(host, sort));
        }
    }
}

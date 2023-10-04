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

namespace Lampac.Controllers.Ebalovo
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("elo")]
        async public Task<JsonResult> Index(string search, string sort, string c, int pg = 1)
        {
            if (!AppInit.conf.Ebalovo.enable)
                return OnError("disable");

            string memKey = $"elo:{search}:{sort}:{c}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("elo", AppInit.conf.Ebalovo);
                var proxy = proxyManager.Get();

                string html = await EbalovoTo.InvokeHtml(AppInit.conf.Ebalovo.host, search, sort, c, pg, url => HttpClient.Get(url, timeoutSeconds: 10, proxy: proxy));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                playlists = EbalovoTo.Playlist($"{host}/elo/vidosik", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));

                memoryCache.Set(memKey, playlists, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 10 : 2));
            }

            return OnResult(playlists, EbalovoTo.Menu(host, sort, c));
        }
    }
}

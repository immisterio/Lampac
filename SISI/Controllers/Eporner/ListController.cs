using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using System;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;

namespace Lampac.Controllers.Eporner
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("epr")]
        async public Task<JsonResult> Index(string search, string sort, string c, int pg = 1)
        {
            var init = AppInit.conf.Eporner;

            if (!init.enable)
                return OnError("disable");

            pg += 1;
            string memKey = $"epr:{search}:{sort}:{c}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("epr", init);
                var proxy = proxyManager.Get();

                string html = await EpornerTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                playlists = EpornerTo.Playlist($"{host}/epr/vidosik", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));

                memoryCache.Set(memKey, playlists, cacheTime(10));
            }

            return OnResult(playlists, string.IsNullOrEmpty(search) ? EpornerTo.Menu(host, sort, c) : null);
        }
    }
}

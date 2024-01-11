using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Models.SISI;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;

namespace Lampac.Controllers.HQporner
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("hqr")]
        async public Task<JsonResult> Index(string search, string sort, string c, int pg = 1)
        {
            var init = AppInit.conf.HQporner;

            if (!init.enable)
                return OnError("disable");

            string memKey = $"hqr:{search}:{sort}:{c}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("hqr", init);
                var proxy = proxyManager.Get();

                string html = await HQpornerTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                playlists = HQpornerTo.Playlist($"{host}/hqr/vidosik", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));

                proxyManager.Success();
                memoryCache.Set(memKey, playlists, cacheTime(10));
            }

            return OnResult(playlists, string.IsNullOrEmpty(search) ? HQpornerTo.Menu(host, sort, c) : null);
        }
    }
}

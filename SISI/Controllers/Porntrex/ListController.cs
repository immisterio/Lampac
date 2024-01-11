using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;

namespace Lampac.Controllers.Porntrex
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("ptx")]
        async public Task<JsonResult> Index(string search, string sort, string c, int pg = 1)
        {
            var init = AppInit.conf.Porntrex;

            if (!init.enable)
                return OnError("disable");

            string memKey = $"ptx:{search}:{sort}:{c}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("ptx", init);
                var proxy = proxyManager.Get();

                string html = await PorntrexTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                playlists = PorntrexTo.Playlist($"{host}/ptx/vidosik", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));

                proxyManager.Success();
                memoryCache.Set(memKey, playlists, cacheTime(10));
            }

            return OnResult(playlists, string.IsNullOrEmpty(search) ? PorntrexTo.Menu(host, sort, c) : null, headers: new List<(string name, string val)> { ("referer", $"{init.host}/") });
        }
    }
}

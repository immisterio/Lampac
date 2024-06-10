using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Shared.Model.Online;

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
            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("ptx", init);
                var proxy = proxyManager.Get();

                string html = await PorntrexTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init)));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                playlists = PorntrexTo.Playlist($"{host}/ptx/vidosik", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager, pg > 1 && string.IsNullOrEmpty(search));

                proxyManager.Success();
                hybridCache.Set(memKey, playlists, cacheTime(10, init: init));
            }

            return OnResult(playlists, PorntrexTo.Menu(host, search, sort, c), headers: HeadersModel.Init("referer", $"{init.host}/"), plugin: "ptx");
        }
    }
}

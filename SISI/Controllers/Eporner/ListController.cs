using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
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

            var proxyManager = new ProxyManager("epr", init);
            var proxy = proxyManager.Get();

            pg += 1;
            var cache = await InvokeCache<List<PlaylistItem>>($"epr:{search}:{sort}:{c}:{pg}", cacheTime(10, init: init), proxyManager, async res => 
            {
                string html = await EpornerTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init)));
                if (html == null)
                    return res.Fail("html");

                var playlists = EpornerTo.Playlist($"{host}/epr/vidosik", html);

                if (playlists.Count == 0)
                    return res.Fail("playlists");

                return playlists;
            });

            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg, proxyManager, pg > 1 && string.IsNullOrEmpty(search));

            return OnResult(cache.Value, EpornerTo.Menu(host, search, sort, c), plugin: "epr");
        }
    }
}

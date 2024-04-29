using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using System.Text.RegularExpressions;

namespace Lampac.Controllers.Xhamster
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("xmr")]
        [Route("xmrgay")]
        [Route("xmrsml")]
        async public Task<JsonResult> Index(string search, string c, string q, string sort = "newest", int pg = 1)
        {
            var init = AppInit.conf.Xhamster;

            if (!init.enable)
                return OnError("disable");

            pg++;
            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            string memKey = $"{plugin}:{search}:{sort}:{c}:{q}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("xmr", init);
                var proxy = proxyManager.Get();

                string html = await XhamsterTo.InvokeHtml(init.corsHost(), plugin, search, c, q, sort, pg, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init)));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                playlists = XhamsterTo.Playlist($"{host}/xmr/vidosik", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager, pg > 1 && string.IsNullOrEmpty(search));

                proxyManager.Success();
                hybridCache.Set(memKey, playlists, cacheTime(10, init: init));
            }

            return OnResult(playlists, string.IsNullOrEmpty(search) ? XhamsterTo.Menu(host, plugin, c, q, sort) : null, plugin: "xmr");
        }
    }
}

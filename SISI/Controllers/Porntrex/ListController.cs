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
        async public Task<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            var init = AppInit.conf.Porntrex.Clone();
            if (!init.enable)
                return OnError("disable");

            if (NoAccessGroup(init, out string error_msg))
                return OnError(error_msg, false);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            string memKey = $"ptx:{search}:{sort}:{c}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("ptx", init);
                var proxy = proxyManager.Get();

                reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                string html = await PorntrexTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url =>
                    rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                );

                playlists = PorntrexTo.Playlist($"{host}/ptx/vidosik", html);

                if (playlists.Count == 0)
                {
                    if (IsRhubFallback(init))
                        goto reset;

                    return OnError("playlists", proxyManager, !rch.enable && string.IsNullOrEmpty(search));
                }

                if (!rch.enable)
                    proxyManager.Success();

                hybridCache.Set(memKey, playlists, cacheTime(10, init: init));
            }

            return OnResult(playlists, PorntrexTo.Menu(host, search, sort, c), headers: HeadersModel.Init("referer", $"{init.host}/"), plugin: "ptx");
        }
    }
}

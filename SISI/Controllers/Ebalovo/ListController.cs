using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Shared.Model.Online;

namespace Lampac.Controllers.Ebalovo
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("elo")]
        async public Task<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.Ebalovo);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            string memKey = $"elo:{search}:{sort}:{c}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager(init);
                var proxy = proxyManager.Get();

                string ehost = await RootController.goHost(init.corsHost());

                reset: var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
                if (rch.IsNotSupport("web", out string rch_error))
                    return OnError(rch_error);

                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                var headers = httpHeaders(init, HeadersModel.Init(
                    ("sec-fetch-dest", "document"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "same-origin"),
                    ("sec-fetch-user", "?1"),
                    ("upgrade-insecure-requests", "1")
                ));

                string html = await EbalovoTo.InvokeHtml(ehost, search, sort, c, pg, url =>
                    rch.enable ? rch.Get(init.cors(url), headers) : HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: headers, configureAwait: AppInit.conf.mikrotik)
                );

                playlists = EbalovoTo.Playlist($"{host}/elo/vidosik", html);

                if (playlists.Count == 0)
                {
                    if (IsRhubFallback(init))
                        goto reset;

                    return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));
                }

                if (!rch.enable)
                    proxyManager.Success();

                hybridCache.Set(memKey, playlists, cacheTime(10, init: init));
            }

            return OnResult(playlists, string.IsNullOrEmpty(search) ? EbalovoTo.Menu(host, sort, c) : null, plugin: init.plugin);
        }
    }
}

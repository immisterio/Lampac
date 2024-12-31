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
        async public Task<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            var init = AppInit.conf.Eporner.Clone();

            if (!init.enable)
                return OnError("disable");

            if (NoAccessGroup(init, out string error_msg))
                return OnError(error_msg, false);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            var proxyManager = new ProxyManager("epr", init);
            var proxy = proxyManager.Get();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web", out string rch_error))
                return OnError(rch_error, false);

            pg += 1;
            var cache = await InvokeCache<List<PlaylistItem>>($"epr:{search}:{sort}:{c}:{pg}", cacheTime(10, init: init), proxyManager, async res => 
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string html = await EpornerTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => 
                    rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                );

                var playlists = EpornerTo.Playlist($"{host}/epr/vidosik", html);

                if (playlists.Count == 0)
                    return res.Fail("playlists");

                return playlists;
            });

            if (!cache.IsSuccess)
            {
                if (cache.ErrorMsg == "playlists" && IsRhubFallback(init))
                    goto reset;

                if (cache.ErrorMsg != null && cache.ErrorMsg.StartsWith("{\"rch\":true,"))
                    return ContentTo(cache.ErrorMsg);

                return OnError(cache.ErrorMsg, proxyManager, !rch.enable && string.IsNullOrEmpty(search));
            }

            return OnResult(cache.Value, EpornerTo.Menu(host, search, sort, c), plugin: "epr");
        }
    }
}

using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Eporner
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("epr")]
        async public ValueTask<ActionResult> Index(string search, string sort, string c, int pg = 1)
        {
            var init = await loadKit(AppInit.conf.Eporner);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotSupport("web", out string rch_error))
                return OnError(rch_error);

            pg += 1;
            var cache = await InvokeCache<List<PlaylistItem>>($"epr:{search}:{sort}:{c}:{pg}", cacheTime(10, init: init), proxyManager, async res => 
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string html = await EpornerTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => 
                    rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : Http.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                );

                var playlists = EpornerTo.Playlist($"{host}/epr/vidosik", html);

                if (playlists.Count == 0)
                    return res.Fail("playlists");

                return playlists;
            }, memory: false);

            if (!cache.IsSuccess)
            {
                if (cache.ErrorMsg == "playlists" && IsRhubFallback(init))
                    goto reset;

                if (cache.ErrorMsg != null && cache.ErrorMsg.StartsWith("{\"rch\":true,"))
                    return ContentTo(cache.ErrorMsg);

                return OnError(cache.ErrorMsg, proxyManager, string.IsNullOrEmpty(search));
            }

            return OnResult(cache.Value, EpornerTo.Menu(host, search, sort, c), plugin: init.plugin);
        }
    }
}

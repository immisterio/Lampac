using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Chaturbate
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.Chaturbate) { }

        [HttpGet]
        [Route("chu")]
        async public ValueTask<ActionResult> Index(string search, string sort, int pg = 1)
        {
            if (!string.IsNullOrEmpty(search))
                return OnError("no search", false);

            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<List<PlaylistItem>>($"Chaturbate:list:{sort}:{pg}", 5, async e =>
            {
                string html = await ChaturbateTo.InvokeHtml(init.corsHost(), sort, pg, 
                    url => httpHydra.Get(url)
                );

                var playlists = ChaturbateTo.Playlist("chu/potok", html);

                if (playlists == null || playlists.Count == 0)
                    return e.Fail("playlists", refresh_proxy: true);

                return e.Success(playlists);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return OnResult(cache, ChaturbateTo.Menu(host, sort));
        }
    }
}

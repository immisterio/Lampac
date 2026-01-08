using Microsoft.AspNetCore.Mvc;
using Shared.Engine.RxEnumerate;
using System.Web;

namespace SISI.Controllers.Tizam
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.Tizam) { }

        [Route("tizam")]
        async public Task<ActionResult> Index(string search, int pg = 1)
        {
            if (!string.IsNullOrEmpty(search))
                return OnError("no search", false);

            if (await IsRequestBlocked(rch: true, rch_keepalive: -1))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<List<PlaylistItem>>($"tizam:{pg}", 60, async e =>
            {
                string uri = $"{init.corsHost()}/fil_my_dlya_vzroslyh/s_russkim_perevodom/";

                int page = pg - 1;
                if (page > 0)
                    uri += $"?p={page}";

                List<PlaylistItem> playlists = null;

                await httpHydra.GetSpan(uri, span => 
                {
                    playlists = Playlist(span);
                });

                if (playlists == null || playlists.Count == 0)
                    return e.Fail("playlists", refresh_proxy: true);

                return e.Success(playlists);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return await PlaylistResult(cache);
        }


        static List<PlaylistItem> Playlist(ReadOnlySpan<char> html)
        {
            if (html.IsEmpty)
                return null;

            var pagination = Rx.Split("id=\"pagination\"", html);
            if (pagination.Count == 0)
                return null;

            var rx = Rx.Split("video-item", pagination[0].Span, 1);
            if (rx.Count == 0)
                return null;

            var playlists = new List<PlaylistItem>(rx.Count);

            foreach (var row in rx.Rows())
            {
                if (row.Contains("pin--premium"))
                    continue;

                string title = row.Match("-name=\"name\">([^<]+)<");
                string href = row.Match("href=\"/([^\"]+)\" itemprop=\"url\"");

                if (!string.IsNullOrEmpty(href) && !string.IsNullOrWhiteSpace(title))
                {
                    string img = row.Match("class=\"item__img\" src=\"/([^\"]+)\"");
                    if (img == null)
                        continue;

                    var pl = new PlaylistItem()
                    {
                        name = title,
                        video = $"tizam/vidosik?uri={HttpUtility.UrlEncode(href)}",
                        picture = $"{AppInit.conf.Tizam.host}/{img}",
                        time = row.Match("itemprop=\"duration\" content=\"([^<]+)\"", trim: true),
                        json = true,
                        bookmark = new Bookmark()
                        {
                            site = "tizam",
                            href = href,
                            image = $"{AppInit.conf.Tizam.host}/{img}"
                        }
                    };

                    playlists.Add(pl);
                }
            }

            return playlists;
        }
    }
}

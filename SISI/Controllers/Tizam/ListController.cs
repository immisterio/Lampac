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

                var playlists = Playlist(await httpHydra.Get(uri));

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
                return new List<PlaylistItem>();

            var pagination = Rx.Split("id=\"pagination\"", html);
            if (pagination.Count == 0)
                return new List<PlaylistItem>();

            var rx = Rx.Split("video-item", pagination.First().Span, 1);

            var playlists = new List<PlaylistItem>(rx.Count);

            foreach (var row in rx.Rows())
            {
                if (row.Contains("pin--premium"))
                    continue;

                string title = row.Match("-name=\"name\">([^<]+)<");
                string href = row.Match("href=\"/([^\"]+)\" itemprop=\"url\"");

                if (!string.IsNullOrEmpty(href) && !string.IsNullOrWhiteSpace(title))
                {
                    string duration = row.Match("itemprop=\"duration\" content=\"([^<]+)\"");

                    string img = row.Match("class=\"item__img\" src=\"/([^\"]+)\"");
                    if (string.IsNullOrEmpty(img))
                        continue;

                    var pl = new PlaylistItem()
                    {
                        name = title,
                        video = $"tizam/vidosik?uri={HttpUtility.UrlEncode(href)}",
                        picture = $"{AppInit.conf.Tizam.host}/{img}",
                        time = duration?.Trim(),
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

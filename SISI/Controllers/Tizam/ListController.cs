using Microsoft.AspNetCore.Mvc;
using System.Web;

namespace SISI.Controllers.Tizam
{
    public class ListController : BaseSisiController
    {
        public ListController() : base(AppInit.conf.Tizam) { }

        [Route("tizam")]
        async public ValueTask<ActionResult> Index(string search, int pg = 1)
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

            return OnResult(cache);
        }


        static List<PlaylistItem> Playlist(string html)
        {
            var playlists = new List<PlaylistItem>() { Capacity = 25 };
            if (string.IsNullOrEmpty(html))
                return playlists;

            foreach (string row in Regex.Split(html.Split("id=\"pagination\"")[0], "video-item").Skip(1))
            {
                if (row.Contains("pin--premium"))
                    continue;

                string title = Regex.Match(row, "-name=\"name\">([^<]+)<").Groups[1].Value;
                string href = Regex.Match(row, "href=\"/([^\"]+)\" itemprop=\"url\"").Groups[1].Value;

                if (!string.IsNullOrEmpty(href) && !string.IsNullOrWhiteSpace(title))
                {
                    string duration = Regex.Match(row, "itemprop=\"duration\" content=\"([^<]+)\"").Groups[1].Value;

                    string img = Regex.Match(row, "class=\"item__img\" src=\"/([^\"]+)\"").Groups[1].Value;
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

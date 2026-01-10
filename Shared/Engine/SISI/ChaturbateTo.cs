using Shared.Engine.RxEnumerate;
using Shared.Models.SISI.Base;
using System.Text;
using System.Threading;

namespace Shared.Engine.SISI
{
    public static class ChaturbateTo
    {
        static readonly ThreadLocal<StringBuilder> sbUri = new(() => new StringBuilder(PoolInvk.rentChunk));

        #region Uri
        public static string Uri(string host, string sort, int pg)
        {
            var url = sbUri.Value;
            url.Clear();

            url.Append(host);
            url.Append("/api/ts/roomlist/room-list/?enable_recommendations=false&limit=90");

            if (!string.IsNullOrWhiteSpace(sort))
                url.Append($"&genders={sort}");

            if (pg > 1)
                url.Append($"&offset={pg * 90}");

            return url.ToString();
        }
        #endregion

        #region Playlist
        public static List<PlaylistItem> Playlist(string route, ReadOnlySpan<char> html, Func<PlaylistItem, PlaylistItem> onplaylist = null)
        {
            if (html.IsEmpty)
                return null;

            var rx = Rx.Split("display_age", html, 1);
            if (rx.Count == 0)
                return null;

            var playlists = new List<PlaylistItem>(rx.Count);

            foreach (var row in rx.Rows())
            {
                if (!row.Contains("\"current_show\":\"public\""))
                    continue;

                string baba = row.Match("\"username\":\"([^\"]+)\"");
                if (string.IsNullOrWhiteSpace(baba))
                    continue;

                string img = row.Match("\"img\":\"([^\"]+)\"");
                if (string.IsNullOrEmpty(img))
                    continue;

                var pl = new PlaylistItem()
                {
                    name = baba.Trim(),
                    //quality = row.Contains(">HD+</div>") ? "HD+" : row.Contains(">HD</div>") ? "HD" : null,
                    video = $"{route}?baba={baba}",
                    picture = img.Replace("\\", ""),
                    json = true
                };

                if (onplaylist != null)
                    pl = onplaylist.Invoke(pl);

                playlists.Add(pl);
            }

            return playlists;
        }
        #endregion

        #region Menu
        public static List<MenuItem> Menu(string host, string sort)
        {
            host = string.IsNullOrWhiteSpace(host) ? string.Empty : $"{host}/";

            var sortmenu = new List<MenuItem>()
            {
                new MenuItem()
                {
                    title = "Лучшие",
                    playlist_url = host + "chu"
                },
                new MenuItem()
                {
                    title = "Девушки",
                    playlist_url = host + "chu?sort=f"
                },
                new MenuItem()
                {
                    title = "Пары",
                    playlist_url = host + "chu?sort=c"
                },
                new MenuItem()
                {
                    title = "Парни",
                    playlist_url = host + "chu?sort=m"
                },
                new MenuItem()
                {
                    title = "Транссексуалы",
                    playlist_url = host + "chu?sort=t"
                }
            };

            return new List<MenuItem>()
            {
                new MenuItem()
                {
                    title = $"Сортировка: {sortmenu.FirstOrDefault(i => i.playlist_url!.EndsWith($"={sort}")).title ?? "Лучшие" }",
                    playlist_url = "submenu",
                    submenu = sortmenu
                }
            };
        }
        #endregion

        #region StreamLinks
        public static string StreamLinksUri(string host, string baba)
        {
            if (string.IsNullOrWhiteSpace(baba))
                return null;

            return $"{host}/{baba}/";
        }

        public static Dictionary<string, string> StreamLinks(ReadOnlySpan<char> html)
        {
            if (html.IsEmpty)
                return null;

            string hls = Rx.Match(html, "(https?://[^ ]+/playlist\\.m3u8)");
            if (hls == null)
                return null;

            return new Dictionary<string, string>()
            {
                ["auto"] = hls.Replace("\\u002D", "-").Replace("\\", "")
            };
        }
        #endregion
    }
}

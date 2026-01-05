using Shared.Engine.RxEnumerate;
using Shared.Models.SISI.Base;
using System.Text.RegularExpressions;

namespace Shared.Engine.SISI
{
    public static class ChaturbateTo
    {
        public static Task<string> InvokeHtml(string host, string sort, int pg, Func<string, Task<string>> onresult)
        {
            string url = host + "/api/ts/roomlist/room-list/?enable_recommendations=false&limit=90";

            if (!string.IsNullOrWhiteSpace(sort))
                url += $"&genders={sort}";

            if (pg > 1)
                url += $"&offset={pg * 90}";

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, ReadOnlySpan<char> html, Func<PlaylistItem, PlaylistItem> onplaylist = null)
        {
            if (html.IsEmpty)
                return new List<PlaylistItem>();

            var rx = Rx.Split("display_age", html, 1);

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
                    video = $"{uri}?baba={baba}",
                    picture = img.Replace("\\", ""),
                    json = true
                };

                if (onplaylist != null)
                    pl = onplaylist.Invoke(pl);

                playlists.Add(pl);
            }

            return playlists;
        }

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

        async public static Task<Dictionary<string, string>> StreamLinks(string host, string baba, Func<string, Task<string>> onresult)
        {
            if (string.IsNullOrWhiteSpace(baba))
                return null;

            string html = await onresult.Invoke($"{host}/{baba}/");
            string hls = Regex.Match(html ?? "", "(https?://[^ ]+/playlist\\.m3u8)").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(hls))
                return null;

            return new Dictionary<string, string>()
            {
                ["auto"] = hls.Replace("\\u002D", "-").Replace("\\", "")
            };
        }
    }
}

using Shared.Models.SISI.Base;
using System.Text.RegularExpressions;

namespace Shared.Engine.SISI
{
    public static class ChaturbateTo
    {
        public static ValueTask<string> InvokeHtml(string host, string sort, int pg, Func<string, ValueTask<string>> onresult)
        {
            string url = host + "/api/ts/roomlist/room-list/?enable_recommendations=false&limit=90";

            if (!string.IsNullOrWhiteSpace(sort))
                url += $"&genders={sort}";

            if (pg > 1)
                url += $"&offset={pg * 90}";

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, in string html, Func<PlaylistItem, PlaylistItem> onplaylist = null)
        {
            if (string.IsNullOrEmpty(html))
                return new List<PlaylistItem>();

            var rows = html.Split("display_age");
            var playlists = new List<PlaylistItem>(rows.Length);

            foreach (string row in rows.Skip(1))
            {
                if (!row.Contains("\"current_show\":\"public\""))
                    continue;

                string baba = Regex.Match(row, "\"username\":\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(baba))
                    continue;

                string img = Regex.Match(row, "\"img\":\"([^\"]+)\"").Groups[1].Value;
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

        async public static ValueTask<Dictionary<string, string>> StreamLinks(string host, string baba, Func<string, ValueTask<string>> onresult)
        {
            if (string.IsNullOrWhiteSpace(baba))
                return null;

            string html = await onresult.Invoke($"{host}/{baba}/");
            string hls = new Regex("(https?://[^ ]+/playlist\\.m3u8)").Match(html ?? "").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(hls))
                return null;

            return new Dictionary<string, string>()
            {
                ["auto"] = hls.Replace("\\u002D", "-").Replace("\\", "")
            };
        }
    }
}

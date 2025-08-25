using Shared.Models.SISI.Base;
using System.Text.RegularExpressions;

namespace Shared.Engine.SISI
{
    public static class RunetkiTo
    {
        public static ValueTask<string> InvokeHtml(string host, string sort, int pg, Func<string, ValueTask<string>> onresult)
        {
            string url = host + $"/tools/listing_v3.php?livetab={sort ?? "all"}&offset={(pg > 1 ? ((pg-1) * 72) : 0)}&limit=72";

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(in string html, out int total_pages, Func<PlaylistItem, PlaylistItem> onplaylist = null)
        {
            total_pages = 0;

            if (string.IsNullOrEmpty(html))
                return new List<PlaylistItem>();

            var rows = html.Split("\"gender\"");
            var playlists = new List<PlaylistItem>(rows.Length);

            foreach (string row in rows.Skip(1))
            {
                string baba = Regex.Match(row, "\"username\":\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(baba))
                    continue;

                string esid = Regex.Match(row, "\"esid\":\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(esid))
                    continue;

                string img = Regex.Match(row, "\"thumb_image\":\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(img))
                    continue;

                string title = Regex.Match(row, "\"display_name\":\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(title))
                    title = baba;

                var pl = new PlaylistItem()
                {
                    name = title,
                    quality = Regex.Match(row, "\"vq\":\"([^\"]+)\"").Groups[1].Value,
                    video = $"https://{esid}.bcvcdn.com/hls/stream_{baba}/playlist.m3u8",
                    picture = $"https:{img.Replace("\\", "").Replace("{ext}", "jpg")}"
                };

                if (onplaylist != null)
                    pl = onplaylist.Invoke(pl);

                playlists.Add(pl);
            }

            string total_count = Regex.Match(html, "\"total_count\":([0-9]+),").Groups[1].Value;
            if (int.TryParse(total_count, out int total) && total > 0)
            {
                if (72 >= total)
                    total_pages = 1;
                else
                    total_pages = (total / 72) + 1;
            }

            return playlists;
        }

        public static List<MenuItem> Menu(string host, string sort)
        {
            host = string.IsNullOrWhiteSpace(host) ? string.Empty : $"{host}/";

            return new List<MenuItem>()
            {
                new MenuItem()
                {
                    title = $"Сортировка: {(string.IsNullOrWhiteSpace(sort) ? "выбрать" : sort)}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Новые",
                            playlist_url = host + "runetki?sort=new"
                        },
                        new MenuItem()
                        {
                            title = "Пары",
                            playlist_url = host + "runetki?sort=couples"
                        },
                        new MenuItem()
                        {
                            title = "Девушки",
                            playlist_url = host + "runetki?sort=female"
                        },
                        new MenuItem()
                        {
                            title = "Парни",
                            playlist_url = host + "runetki?sort=male"
                        },
                        new MenuItem()
                        {
                            title = "Транссексуалы",
                            playlist_url = host + "runetki?sort=transsexual"
                        }
                    }
                }
            };
        }
    }
}

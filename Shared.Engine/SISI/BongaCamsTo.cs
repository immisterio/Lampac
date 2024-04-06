using Lampac.Models.SISI;
using System.Text.RegularExpressions;

namespace Shared.Engine.SISI
{
    public static class BongaCamsTo
    {
        public static ValueTask<string?> InvokeHtml(string host, string? sort, int pg, Func<string, ValueTask<string?>> onresult)
        {
            string url = host + $"/tools/listing_v3.php?livetab={sort ?? "all"}&limit=72";

            if (pg > 1)
                url += $"&offset={pg * 72}";

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string? html, Func<PlaylistItem, PlaylistItem>? onplaylist = null)
        {
            var playlists = new List<PlaylistItem>() { Capacity = 75 };

            if (string.IsNullOrEmpty(html))
                return playlists;

            foreach (string row in html.Split("\"gender\"").Skip(1))
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
                    video = $"https://{esid}.bcvcdn.com/hls/stream_{baba}/public-aac/stream_{baba}/chunks.m3u8",
                    picture = $"https:{img.Replace("\\", "").Replace("{ext}", "jpg")}"
                };

                if (onplaylist != null)
                    pl = onplaylist.Invoke(pl);

                playlists.Add(pl);
            }

            return playlists;
        }

        public static List<MenuItem> Menu(string? host, string? sort)
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
                            playlist_url = host + "bgs?sort=new"
                        },
                        new MenuItem()
                        {
                            title = "Пары",
                            playlist_url = host + "bgs?sort=couples"
                        },
                        new MenuItem()
                        {
                            title = "Девушки",
                            playlist_url = host + "bgs?sort=female"
                        },
                        new MenuItem()
                        {
                            title = "Парни",
                            playlist_url = host + "bgs?sort=male"
                        },
                        new MenuItem()
                        {
                            title = "Транссексуалы",
                            playlist_url = host + "bgs?sort=transsexual"
                        }
                    }
                }
            };
        }
    }
}

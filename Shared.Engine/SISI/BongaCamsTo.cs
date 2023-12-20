using Lampac.Models.SISI;
using System.Text.RegularExpressions;

namespace Shared.Engine.SISI
{
    public static class BongaCamsTo
    {
        public static ValueTask<string?> InvokeHtml(string host, string? sort, int pg, Func<string, ValueTask<string?>> onresult)
        {
            string url = host;

            if (!string.IsNullOrWhiteSpace(sort))
                url += $"/{sort}";

            if (pg > 1)
                url += $"?page={pg}";

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string? html, Func<PlaylistItem, PlaylistItem>? onplaylist = null)
        {
            var playlists = new List<PlaylistItem>() { Capacity = 100 };

            if (string.IsNullOrEmpty(html))
                return playlists;

            foreach (string row in Regex.Split(html, "class=\"(ls_thumb js-ls_thumb|mls_item mls_so_)").Skip(1))
            {
                string baba = Regex.Match(row, "data-chathost=\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(baba))
                    continue;

                string esid = Regex.Match(row, "data-esid=\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(esid))
                    continue;

                string img = Regex.Match(row, "this.src='//([^']+\\.jpg)'").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(img))
                    img = Regex.Match(row, "src=\"//([^\"]+)\"").Groups[1].Value;

                if (string.IsNullOrWhiteSpace(img))
                    continue;

                string title = Regex.Match(row, "lst_topic lst_data\">([^\n\r<]+)").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(title))
                    title = baba;

                var pl = new PlaylistItem()
                {
                    name = title,
                    quality = row.Contains("__hd_plus __rt") ? "HD+" : row.Contains("__hd __rtl") ? "HD" : null,
                    video = $"https://{esid}.bcvcdn.com/hls/stream_{baba}/public-aac/stream_{baba}/chunks.m3u8",
                    picture = $"https://{img}"
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
                            playlist_url = host + "bgs?sort=new-models"
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
                            playlist_url = host + "bgs?sort=trans"
                        }
                    }
                }
            };
        }
    }
}

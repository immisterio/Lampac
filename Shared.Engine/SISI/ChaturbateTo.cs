using Lampac.Models.SISI;
using Shared.Model;
using System;
using System.Text.RegularExpressions;

namespace Shared.Engine.SISI
{
    public static class ChaturbateTo
    {
        public static ValueTask<string?> InvokeHtml(string host, string? sort, int pg, Func<string, ValueTask<string?>> onresult)
        {
            string url = host;

            if (!string.IsNullOrWhiteSpace(sort))
                url += $"/{sort}/";

            if (pg > 1)
                url += $"?page={pg}";

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, string html, Func<string, string> onpicture)
        {
            var playlists = new List<PlaylistItem>();

            foreach (string row in html.Split("class=\"room_list_room").Skip(1))
            {
                if (row.Contains(">Private</li>"))
                    continue;

                string baba = new Regex("data-room=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(baba))
                {
                    baba = new Regex("class=\"broadcaster-cell\" href=\"/([^/]+)/\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();
                    if (string.IsNullOrWhiteSpace(baba))
                        continue;
                }

                playlists.Add(new PlaylistItem()
                {
                    name = baba,
                    quality = row.Contains(">HD+</div>") ? "HD+" : row.Contains(">HD</div>") ? "HD" : null,
                    video = $"{uri}?baba={baba}",
                    picture = onpicture.Invoke(new Regex(" src=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value),
                    json = true
                });
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
                    title = $"Сортировка: {(string.IsNullOrWhiteSpace(sort) ? "лучшие" : sort)}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Лучшие",
                            playlist_url = host + "chu"
                        },
                        new MenuItem()
                        {
                            title = "Девушки",
                            playlist_url = host + "chu?sort=female-cams"
                        },
                        new MenuItem()
                        {
                            title = "Пары",
                            playlist_url = host + "chu?sort=couple-cams"
                        },
                        new MenuItem()
                        {
                            title = "Парни",
                            playlist_url = host + "chu?sort=male-cams"
                        },
                        new MenuItem()
                        {
                            title = "Транссексуалы",
                            playlist_url = host + "chu?sort=trans-cams"
                        }
                    }
                }
            };
        }

        async public static ValueTask<Dictionary<string, string>?> StreamLinks(string host, string? baba, Func<string, ValueTask<string?>> onresult)
        {
            if (string.IsNullOrWhiteSpace(baba))
                return null;

            string? html = await onresult.Invoke($"{host}/{baba}/");
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

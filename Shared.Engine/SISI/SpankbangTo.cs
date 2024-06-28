using Lampac.Models.SISI;
using Shared.Model.SISI;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class SpankbangTo
    {
        public static ValueTask<string?> InvokeHtml(string host, string? search, string? sort, int pg, Func<string, ValueTask<string?>> onresult)
        {
            string url = $"{host}/";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url += $"s/{HttpUtility.UrlEncode(search)}/{pg}/";
            }
            else
            {
                url += $"{sort ?? "new_videos"}/{pg}/";

                if (sort == "most_popular")
                    url += "?p=m";
            }

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, string? html, Func<PlaylistItem, PlaylistItem>? onplaylist = null)
        {
            var playlists = new List<PlaylistItem>() { Capacity = 105 };

            if (string.IsNullOrEmpty(html))
                return playlists;

            foreach (string row in html.Split("<div class=\"video-item").Skip(1))
            {
                if (!row.Contains("<div class=\"stats\">"))
                    continue;

                var g = Regex.Match(row, "<a href=\"/([^\"]+)\" title=\"([^\"]+)\"").Groups;
                string link = g[1].Value;
                string title = g[2].Value;

                if (!string.IsNullOrWhiteSpace(link) && !string.IsNullOrWhiteSpace(title))
                {
                    string quality = Regex.Match(row, "<span class=\"video-badge h\">([^<]+)</span>").Groups[1].Value;
                    string duration = Regex.Match(row, "<span class=\"video-badge l\">([^<]+)</span>").Groups[1].Value.Trim();
                    string img = Regex.Match(row, "data-src=\"([^\"]+)\"").Groups[1].Value;
                    img = Regex.Replace(img, "/w:[0-9]00/", "/w:300/");

                    var pl = new PlaylistItem()
                    {
                        name = title,
                        video = $"{uri}?uri={link}",
                        quality = string.IsNullOrEmpty(quality) ? null : quality,
                        picture = img,
                        preview = Regex.Match(row, "data-preview=\"([^\"]+)\"").Groups[1].Value,
                        time = duration,
                        json = true,
                        related = true,
                        bookmark = new Bookmark()
                        {
                            site = "sbg",
                            href = link,
                            image = img
                        }
                    };

                    if (onplaylist != null)
                        pl = onplaylist.Invoke(pl);

                    playlists.Add(pl);
                }
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
                    title = "Поиск",
                    search_on = "search_on",
                    playlist_url = host + "sbg",
                },
                new MenuItem()
                {
                    title = $"Сортировка: {(string.IsNullOrWhiteSpace(sort) ? "новое" : sort)}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Новое",
                            playlist_url = host + "sbg"
                        },
                        new MenuItem()
                        {
                            title = "Трендовое",
                            playlist_url = host + "sbg?sort=trending_videos"
                        },
                        new MenuItem()
                        {
                            title = "Популярное",
                            playlist_url = host + "sbg?sort=most_popular"
                        }
                    }
                }
            };
        }

        async public static ValueTask<StreamItem?> StreamLinks(string uri, string host, string? url, Func<string, ValueTask<string?>> onresult)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            string? html = await onresult.Invoke($"{host}/{url}");
            if (string.IsNullOrEmpty(html))
                return null;

            var stream_links = new Dictionary<int, string>();

            var match = new Regex("'([0-9]+)(p|k)': ?\\[\'(https?://[^']+)\'").Match(html);
            while (match.Success)
            {
                int q = $"{match.Groups[1].Value}{match.Groups[2].Value}" == "4k" ? 2160 : int.Parse(match.Groups[1].Value);
                stream_links.TryAdd(q, match.Groups[3].Value);
                match = match.NextMatch();
            }

            return new StreamItem()
            {
                qualitys = stream_links.OrderByDescending(i => i.Key).ToDictionary(k => $"{k.Key}p", v => v.Value),
                recomends = Playlist(uri, html)
            };
        }
    }
}

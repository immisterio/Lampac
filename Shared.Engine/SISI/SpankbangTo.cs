using Lampac.Engine.CORE;
using Lampac.Models.SISI;
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

        public static List<PlaylistItem> Playlist(string uri, string html, Func<string, string> onpicture)
        {
            var playlists = new List<PlaylistItem>();

            foreach (string row in Regex.Split(html, "<div class=\"video-item").Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row) || !row.Contains("<div class=\"stats\">"))
                    continue;

                string link = Regex.Match(row, "<a href=\"/([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
                string title = Regex.Match(row, "class=\"(n|name)\">([^<]+)<", RegexOptions.IgnoreCase).Groups[1].Value;

                if (!string.IsNullOrWhiteSpace(link) && !string.IsNullOrWhiteSpace(title))
                {
                    string quality = new Regex("<span class=\"h\">([^<]+)</span>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;
                    string duration = new Regex("<span class=\"l\">([^<]+)</span>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();
                    string img = new Regex("data-src=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;
                    img = Regex.Replace(img, "/w:[0-9]00/", "/w:300/");

                    playlists.Add(new PlaylistItem()
                    {
                        name = title,
                        video = $"{uri}?uri={HttpUtility.UrlEncode(link)}",
                        quality = string.IsNullOrWhiteSpace(quality) ? null : quality,
                        picture = onpicture.Invoke(img),
                        time = duration,
                        json = true
                    });
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

        async public static ValueTask<Dictionary<string, string>?> StreamLinks(string host, string? uri, Func<string, ValueTask<string?>> onresult)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return null;

            string? html = await onresult.Invoke($"{host}/{uri}");
            string? stream_data = StringConvert.FindLastText(html ?? "", "stream_data", "</script>");

            if (string.IsNullOrWhiteSpace(stream_data))
                return null;

            var stream_links = new Dictionary<string, string>();

            var match = new Regex("'([0-9]+)(p|k)': ?\\[\'(https?://[^']+)\'").Match(stream_data);
            while (match.Success)
            {
                stream_links.TryAdd($"{match.Groups[1].Value}{match.Groups[2].Value}", match.Groups[3].Value);
                match = match.NextMatch();
            }

            return stream_links.OrderByDescending(i => i.Key == "4k").ThenByDescending(i => int.Parse(i.Key.Replace("p", "").Replace("k", ""))).ToDictionary(k => k.Key, v => v.Value);
        }
    }
}

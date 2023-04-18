using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class XhamsterTo
    {
        public static ValueTask<string?> InvokeHtml(string host, string? search, string? sort, int pg, Func<string, ValueTask<string?>> onresult)
        {
            string url = $"{host}/{pg}";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url = $"{host}/search/{HttpUtility.UrlEncode(search)}?page={pg}";
            }
            else
            {
                if (sort == "newest")
                    url = $"{host}/newest/{pg}";

                if (sort == "best")
                    url = $"{host}/best/weekly/{pg}";
            }

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, string html, Func<string, string> onpicture)
        {
            var playlists = new List<PlaylistItem>();

            string section = StringConvert.FindLastText(html, "mixed-section") ?? html;

            foreach (string row in Regex.Split(section, "(<div class=\"thumb-list__item video-thumb|thumb-list-mobile-item)").Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row) || row.Contains("badge_premium"))
                    continue;

                var g = new Regex("__nam[^\"]+\" href=\"https?://[^/]+/([^\"]+)\"([^>]+)?>(<!--[^-]+-->)?([^<]+)", RegexOptions.IgnoreCase).Match(row).Groups;
                string title = g[4].Value;
                string href = g[1].Value;

                if (!string.IsNullOrWhiteSpace(href) && !string.IsNullOrWhiteSpace(title))
                {
                    string duration = new Regex("<div class=\"thumb-image-container__duration\">([^<]+)</div>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();
                    if (string.IsNullOrWhiteSpace(duration))
                    {
                        duration = new Regex("<span data-role-video-duration>([^<]+)</span>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();
                        if (string.IsNullOrWhiteSpace(duration))
                            duration = new Regex("datetime=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();
                    }

                    string img = new Regex("class=\"thumb-image-container__image\" src=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(img))
                        img = new Regex("<noscript><img src=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();

                    playlists.Add(new PlaylistItem()
                    {
                        name = title,
                        video = $"{uri}?uri={HttpUtility.UrlEncode(href)}",
                        picture = onpicture.Invoke(img),
                        quality = row.Contains("-hd") ? "HD" : row.Contains("-uhd") ? "4K" : null,
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
                    playlist_url = host + "xmr",
                },
                new MenuItem()
                {
                    title = $"Сортировка: {(string.IsNullOrWhiteSpace(sort) || sort == "trend" ? "в тренде" : sort)}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "В тренде",
                            playlist_url = host + "xmr?sort=trend"
                        },
                        new MenuItem()
                        {
                            title = "Самые новые",
                            playlist_url = host + "xmr?sort=newest"
                        },
                        new MenuItem()
                        {
                            title = "Лучшие видео",
                            playlist_url = host + "xmr?sort=best"
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
            string stream_link = new Regex("\"hls\":{\"url\":\"([^\"]+)\"").Match(html ?? "").Groups[1].Value.Replace("\\", "");
            if (string.IsNullOrWhiteSpace(stream_link))
                return null;

            if (stream_link.StartsWith("/"))
                stream_link = host + stream_link;

            if (!stream_link.Contains(".m3u"))
                return null;

            return new Dictionary<string, string>()
            {
                ["auto"] = stream_link
            };
        }
    }
}

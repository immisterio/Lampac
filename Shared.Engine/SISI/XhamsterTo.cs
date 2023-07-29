using Lampac.Models.SISI;
using Shared.Model;
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

        public static List<PlaylistItem> Playlist(string uri, string html, Func<PlaylistItem, PlaylistItem>? onplaylist = null)
        {
            var playlists = new List<PlaylistItem>() { Capacity = 50 };

            string section = html.Contains("mixed-section") ? html.Split("mixed-section")[1] : html;

            foreach (string row in Regex.Split(section, "(<div class=\"thumb-list__item video-thumb|thumb-list-mobile-item)").Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row) || row.Contains("badge_premium"))
                    continue;

                var g = Regex.Match(row, "__nam[^\"]+\" href=\"https?://[^/]+/([^\"]+)\"([^>]+)?>(<!--[^-]+-->)?([^<]+)").Groups;
                string title = g[4].Value;
                string href = g[1].Value;

                if (!string.IsNullOrEmpty(href) && !string.IsNullOrWhiteSpace(title))
                {
                    string duration = Regex.Match(row, "<div class=\"thumb-image-container__duration\">([^<]+)</div>").Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(duration))
                    {
                        duration = Regex.Match(row, "<span data-role-video-duration>([^<]+)</span>").Groups[1].Value;
                        if (string.IsNullOrWhiteSpace(duration))
                            duration = Regex.Match(row, "datetime=\"([^\"]+)\"").Groups[1].Value;
                    }

                    string img = Regex.Match(row, "thumb-image-container__image\" src=\"([^\"]+)\"").Groups[1].Value;
                    if (!img.StartsWith("http"))
                        img = Regex.Match(row, "<noscript><img src=\"([^\"]+)\"").Groups[1].Value.Trim();

                    if (!img.StartsWith("http"))
                        continue;

                    var pl = new PlaylistItem()
                    {
                        name = title,
                        video = $"{uri}?uri={href}",
                        picture = img,
                        quality = row.Contains("-hd") ? "HD" : row.Contains("-uhd") ? "4K" : null,
                        time = duration?.Trim(),
                        json = true
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

        async public static ValueTask<StreamItem?> StreamLinks(string uri, string host, string? url, Func<string, ValueTask<string?>> onresult)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            string? html = await onresult.Invoke($"{host}/{url}");
            if (html == null)
                return null;

            string stream_link = Regex.Match(html, "\"h264\":\\[\\{\"url\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
            if (!stream_link.Contains(".m3u"))
                return null;

            if (stream_link.StartsWith("/"))
                stream_link = host + stream_link;

            return new StreamItem()
            {
                qualitys = new Dictionary<string, string>()
                {
                    ["auto"] = stream_link
                },
                recomends = Playlist(uri, html, pl =>
                {
                    pl.picture = $"{AppInit.rsizehost}/recomends/{pl.picture}";
                    return pl;
                })
            };
        }
    }
}

using Lampac.Models.SISI;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class HQpornerTo
    {
        public static ValueTask<string?> InvokeHtml(string host, string? search, string? sort, int pg, Func<string, ValueTask<string?>> onresult)
        {
            string url = $"{host}/";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url += $"?q={HttpUtility.UrlEncode(search)}&p={pg}";
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(sort))
                    url += $"top/{sort}";

                else
                    url += "hdporn";

                url += $"/{pg}";
            }

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, string html, Func<string, string> onpicture)
        {
            var playlists = new List<PlaylistItem>();

            foreach (string row in html.Split("<div class=\"img-container\">").Skip(1))
            {
                var g = new Regex("href=\"/([^\"]+)\" class=\"atfib\"><img src=\"//([^\"]+)\"[^>]+ alt=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups;
                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                {
                    string duration = new Regex("class=\"fa fa-clock-o\" [^>]+></i>([^<]+)", RegexOptions.IgnoreCase).Match(Regex.Replace(row, "[\n\r\t]+", "")).Groups[1].Value.Trim();

                    playlists.Add(new PlaylistItem()
                    {
                        name = g[3].Value.Trim(),
                        video = $"{uri}?uri={HttpUtility.UrlEncode(g[1].Value)}",
                        picture = onpicture.Invoke("https://" + g[2].Value),
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
                    playlist_url = host + "hqr",
                },
                new MenuItem()
                {
                    title = $"Сортировка: {(string.IsNullOrWhiteSpace(sort) ? "новинки" : sort)}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Самые новые",
                            playlist_url = host + "hqr"
                        },
                        new MenuItem()
                        {
                            title = "Топ недели",
                            playlist_url = host + "hqr?sort=week"
                        },
                        new MenuItem()
                        {
                            title = "Топ месяца",
                            playlist_url = host + "hqr?sort=month"
                        }
                    }
                }
            };
        }

        async public static ValueTask<Dictionary<string, string>?> StreamLinks(string host, string? uri, Func<string, ValueTask<string?>> onresult, Func<string, ValueTask<string?>> oniframe)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return null;

            string? html = await onresult.Invoke($"{host}/{uri}");
            if (html == null)
                return null;

            string uriframe = new Regex("<iframe src=\"//([^/]+/video/[^/]+/)\"").Match(html).Groups[1].Value;
            if (string.IsNullOrWhiteSpace(uriframe))
                return null;

            string? iframeHtml = await oniframe.Invoke($"https://{uriframe}");
            if (iframeHtml == null)
                return null;

            var stream_links = new Dictionary<string, string>();
            var match = new Regex("src=\"//([^\"]+)\" title=\"([^\"]+)\"").Match(iframeHtml.Replace("\\", ""));
            while (match.Success)
            {
                if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[2].Value) && !match.Groups[2].Value.Contains("Default"))
                {
                    string hls = "https://" + match.Groups[1].Value;
                    stream_links.TryAdd(match.Groups[2].Value, hls);
                }

                match = match.NextMatch();
            }

            return stream_links.Reverse().ToDictionary(k => k.Key, v => v.Value);
        }
    }
}

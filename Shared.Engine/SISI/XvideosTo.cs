using Lampac.Models.SISI;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class XvideosTo
    {
        public static ValueTask<string?> InvokeHtml(string host, string? search, int pg, Func<string, ValueTask<string?>> onresult)
        {
            string url = $"{host}/new/{pg}";
            if (!string.IsNullOrWhiteSpace(search))
                url = $"{host}/?k={HttpUtility.UrlEncode(search)}&p={pg}";

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, string html, Func<string, string> onpicture)
        {
            var playlists = new List<PlaylistItem>();

            foreach (string row in Regex.Split(html, "<div class=\"thumb-inside\">").Skip(1))
            {
                var g = new Regex($"<a href=\"/(prof-video-click/[^\"]+|video[0-9]+/[^\"]+)\" title=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups;
                string qmark = new Regex("<span class=\"video-hd-mark\">([^<]+)</span>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;

                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                {
                    string duration = new Regex("<span class=\"duration\">([^<]+)</span>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();

                    string img = new Regex("data-src=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;
                    img = Regex.Replace(img, "/videos/thumbs([0-9]+)/", "/videos/thumbs$1lll/");
                    img = Regex.Replace(img, "\\.THUMBNUM\\.(jpg|png)$", ".1.$1", RegexOptions.IgnoreCase);

                    playlists.Add(new PlaylistItem()
                    {
                        name = g[2].Value,
                        video = $"{uri}?uri={HttpUtility.UrlEncode(g[1].Value)}",
                        picture = onpicture.Invoke(img),
                        quality = string.IsNullOrWhiteSpace(qmark) ? null : qmark,
                        time = duration,
                        json = true
                    });
                }
            }

            return playlists;
        }

        public static List<MenuItem> Menu(string? host)
        {
            host = string.IsNullOrWhiteSpace(host) ? string.Empty : $"{host}/";

            return new List<MenuItem>()
            {
                new MenuItem()
                {
                    title = "Поиск",
                    search_on = "search_on",
                    playlist_url = host + "xds",
                }
            };
        }

        async public static ValueTask<Dictionary<string, string>?> StreamLinks(string host, string? uri, Func<string, ValueTask<string?>> onresult, Func<string, ValueTask<string?>> onm3u)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return null;

            string? html = await onresult.Invoke($"{host}/{Regex.Replace(uri ?? "", "^([^/]+)/.*", "$1/_")}");
            string stream_link = new Regex("html5player\\.setVideoHLS\\('([^']+)'\\);").Match(html ?? "").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(stream_link))
                return null;

            string? m3u8 = await onm3u.Invoke(stream_link);
            if (m3u8 == null)
            {
                return new Dictionary<string, string>()
                {
                    ["auto"] = stream_link
                };
            }

            var stream_links = new Dictionary<int, string>();

            foreach (string line in m3u8.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("hls-"))
                    continue;

                string _q = new Regex("hls-([0-9]+)p").Match(line).Groups[1].Value;

                if (int.TryParse(_q, out int q) && q > 0)
                    stream_links.TryAdd(q, $"{Regex.Replace(stream_link, "/hls.m3u8.*", "")}/{line}");
            }

            return stream_links.OrderByDescending(i => i.Key).ToDictionary(k => $"{k.Key}p", v => v.Value);
        }
    }
}

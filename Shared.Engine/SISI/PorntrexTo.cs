using Lampac.Models.SISI;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class PorntrexTo
    {
        public static ValueTask<string?> InvokeHtml(string host, string? search, string? sort, int pg, Func<string, ValueTask<string?>> onresult)
        {
            string url = $"{host}/";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url = $"{host}/search/{HttpUtility.UrlEncode(search)}/latest-updates/?from_videos={pg}";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(sort))
                {
                    url += $"latest-updates/{pg}/";
                }
                else
                {
                    url += $"{sort}/weekly/?from4={pg}";
                }
            }

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, string html, Func<string, string> onpicture)
        {
            var playlists = new List<PlaylistItem>();

            foreach (string row in Regex.Split(html, "<div class=\"video-preview-screen").Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row) || row.Contains("<span class=\"line-private\">"))
                    continue;

                var g = new Regex($"<a href=\"https?://[^/]+/(video/[^\"]+)\" title=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups;
                string quality = new Regex("<span class=\"quality\">([^<]+)</span>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;

                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                {
                    string duration = new Regex("<i class=\"fa fa-clock-o\"></i>([^<]+)</div>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();
                    var img = new Regex("data-src=\"(https?:)?//((statics.cdntrex.com/contents/videos_screenshots/[0-9]+/[0-9]+)[^\"]+)", RegexOptions.IgnoreCase).Match(row).Groups;

                    playlists.Add(new PlaylistItem()
                    {
                        video = $"{uri}?uri={HttpUtility.UrlEncode(g[1].Value)}",
                        name = g[2].Value,
                        picture = onpicture.Invoke($"https://{img[2].Value}"),
                        quality = !string.IsNullOrEmpty(quality) ? quality : null,
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
                    playlist_url = host + "ptx",
                },
                new MenuItem()
                {
                    title = $"Сортировка: {(string.IsNullOrWhiteSpace(sort) ? "новинки" : sort)}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Новинки",
                            playlist_url = host + "ptx"
                        },
                        new MenuItem()
                        {
                            title = "Топ просмотров",
                            playlist_url = host + "ptx?sort=most-popular"
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
            if (html == null)
                return null;

            var stream_links = new Dictionary<string, string>();
            var match = new Regex("(https?://[^/]+/get_file/[^\\.]+_([0-9]+p)\\.mp4)").Match(html);
            while (match.Success)
            {
                stream_links.TryAdd(match.Groups[2].Value, match.Groups[1].Value);
                match = match.NextMatch();
                //break;
            }

            if (stream_links.Count == 0)
            {
                string link = Regex.Match(html, "(https?://[^/]+/get_file/[^\\.]+\\.mp4)").Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(link))
                    stream_links.TryAdd("auto", link);
            }

            return stream_links.Reverse().ToDictionary(k => k.Key, v => v.Value);
        }
    }
}

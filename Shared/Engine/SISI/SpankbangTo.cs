using Shared.Models.SISI.Base;
using Shared.Models.SISI.OnResult;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class SpankbangTo
    {
        public static Task<string> InvokeHtml(string host, string search, string sort, int pg, Func<string, Task<string>> onresult)
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

        public static List<PlaylistItem> Playlist(string uri, string html, Func<PlaylistItem, PlaylistItem> onplaylist = null)
        {
            if (string.IsNullOrEmpty(html))
                return new List<PlaylistItem>();

            if (html.Contains("class=\"main-container\""))
                html = html.Split("class=\"main-container\"")[1];

            var nodes = HtmlParse.Nodes(html, "//div[@data-testid='video-item']");
            var playlists = new List<PlaylistItem>(nodes.Count);

            foreach (var node in nodes)
            {
                var g = Regex.Match(node.row.InnerHtml, "<a href=\"/(?<link>[^\"]+)\" title=\"(?<title>[^\"]+)\"", RegexOptions.Compiled).Groups;
                if (!string.IsNullOrWhiteSpace(g["link"].Value) && !string.IsNullOrWhiteSpace(g["title"].Value))
                {
                    #region image
                    string img = node.Regex("([\n\r\t ]+)src=\"([^\"]+)\"", 2);
                    if (!img.Contains("/w:"))
                        img = node.Regex("data-src=\"([^\"]+)\"");

                    img = Regex.Replace(img, "/w:[0-9]00/", "/w:300/", RegexOptions.Compiled);
                    #endregion

                    string preview = node.Regex("data-preview=\"([^\"]+)\"");
                    if (string.IsNullOrEmpty(preview))
                        preview = node.Regex("<source data-src=\"([^\"]+)\"");

                    var pl = new PlaylistItem()
                    {
                        name = g["title"].Value,
                        video = $"{uri}?uri={g["link"].Value}",
                        quality = node.SelectText(".//*[@data-testid='video-item-resolution']"),
                        picture = img,
                        preview = preview,
                        time = node.SelectText(".//*[@data-testid='video-item-length']"),
                        json = true,
                        related = true,
                        bookmark = new Bookmark()
                        {
                            site = "sbg",
                            href = g["link"].Value,
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

        public static List<MenuItem> Menu(string host, string sort)
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

        async public static Task<StreamItem> StreamLinks(string uri, string host, string url, Func<string, Task<string>> onresult)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            string html = await onresult.Invoke($"{host}/{url}");
            if (string.IsNullOrEmpty(html))
                return null;

            var stream_links = new Dictionary<int, string>();

            var match = Regex.Match(html, "'([0-9]+)(p|k)': ?\\[\'(https?://[^']+)\'", RegexOptions.Compiled);
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

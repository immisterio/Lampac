using Lampac.Models.SISI;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class EbalovoTo
    {
        public static ValueTask<string?> InvokeHtml(string host, string? search, string? sort, int pg, Func<string, ValueTask<string?>> onresult)
        {
            string url = $"{host}/";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url += $"search/{HttpUtility.UrlEncode(search)}/";
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(sort))
                    url += $"{sort}/";
            }

            if (pg > 1)
                url += $"{pg}/";

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, string html, Func<string, string> onpicture)
        {
            var playlists = new List<PlaylistItem>();

            foreach (string row in Regex.Split(Regex.Replace(html, "[\n\r\t]+", ""), "<div class=\"item\">"))
            {
                if (string.IsNullOrWhiteSpace(row) || !row.Contains("<div class=\"item-info\">"))
                    continue;

                string link = new Regex($"<a href=\"https?://[^/]+/(video/[^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;
                string title = new Regex("<div class=\"item-title\">([^<]+)</div>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();

                if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(link))
                {
                    string duration = new Regex(" data-eb=\"([^;\"]+);", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();
                    var img = new Regex("( )src=\"(([^\"]+)/[0-9]+.jpg)\"", RegexOptions.IgnoreCase).Match(row).Groups;
                    if (string.IsNullOrWhiteSpace(img[3].Value) || img[2].Value.Contains("load.png"))
                        img = new Regex("(data-srcset|data-src|srcset)=\"([^\"]+/[0-9]+.jpg)\"", RegexOptions.IgnoreCase).Match(row).Groups;

                    playlists.Add(new PlaylistItem()
                    {
                        name = title,
                        video = $"{uri}?uri={HttpUtility.UrlEncode(link)}",
                        picture = onpicture.Invoke(img[2].Value),
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
                    playlist_url = host + "elo",
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
                            playlist_url = host + "elo"
                        },
                        new MenuItem()
                        {
                            title = "Лучшее",
                            playlist_url = host + "elo?sort=porno-online"
                        },
                        new MenuItem()
                        {
                            title = "Популярное",
                            playlist_url = host + "elo?sort=xxx-top"
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

            string? stream_link = null;
            var match = new Regex($"(https?://[^/]+/get_file/[^\\.]+_([0-9]+p)\\.mp4)").Match(html ?? "");
            while (match.Success)
            {
                stream_link = match.Groups[1].Value;
                match = match.NextMatch();
            }

            if (string.IsNullOrWhiteSpace(stream_link))
                return null;

            return new Dictionary<string, string>()
            {
                ["auto"] = stream_link
            };
        }
    }
}

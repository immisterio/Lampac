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

        public static List<PlaylistItem> Playlist(string uri, string html, Func<PlaylistItem, PlaylistItem>? onplaylist = null)
        {
            var playlists = new List<PlaylistItem>() { Capacity = 35 };

            foreach (string row in html.Split("<div class=\"item\">"))
            {
                if (!row.Contains("<div class=\"item-info\">"))
                    continue;

                string link = Regex.Match(row, "<a href=\"https?://[^/]+/(video/[^\"]+)\"").Groups[1].Value;
                string title = Regex.Match(row, "<div class=\"item-title\">([^<]+)</div>").Groups[1].Value;

                if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(link))
                {
                    string duration = Regex.Match(row, " data-eb=\"([^;\"]+);").Groups[1].Value.Trim();
                    var img = Regex.Match(row, "( )src=\"(([^\"]+)/[0-9]+.jpg)\"").Groups;
                    if (string.IsNullOrWhiteSpace(img[3].Value) || img[2].Value.Contains("load.png"))
                        img = Regex.Match(row, "(data-srcset|data-src|srcset)=\"([^\"]+/[0-9]+.jpg)\"").Groups;

                    var pl = new PlaylistItem()
                    {
                        name = title.Trim(),
                        video = $"{uri}?uri={link}",
                        picture = img[2].Value,
                        time = duration,
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

        async public static ValueTask<StreamItem?> StreamLinks(string uri, string host, string? url, Func<string, ValueTask<string?>> onresult, Func<string, ValueTask<string?>>? onlocation = null)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            string? html = await onresult.Invoke($"{host}/{url}");
            if (html == null)
                return null;

            string? stream_link = null;
            var match = new Regex("(https?://[^/]+/get_file/[^\\.]+_([0-9]+p)\\.mp4)").Match(html);
            while (match.Success)
            {
                stream_link = match.Groups[1].Value;
                match = match.NextMatch();
            }

            if (string.IsNullOrEmpty(stream_link))
                return null;

            if (onlocation != null)
            {
                string? location = await onlocation.Invoke(stream_link);
                if (location == null || stream_link == location || location.Contains("/get_file/"))
                    return null;

                stream_link = location;
            }

            return new StreamItem()
            {
                qualitys = new Dictionary<string, string>()
                {
                    ["auto"] = stream_link
                },
                recomends = Playlist(uri, html)
            };
        }
    }
}

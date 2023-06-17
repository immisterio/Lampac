using Lampac.Models.SISI;
using Shared.Model;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class EpornerTo
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
            var playlists = new List<PlaylistItem>() { Capacity = 70 };

            foreach (string row in Regex.Split(html, "<div class=\"mb( hdy)?\"").Skip(1))
            {
                var g = Regex.Match(row, "<p class=\"mbtit\"><a href=\"/([^\"]+)\">([^<]+)</a>").Groups;
                string quality = Regex.Match(row, "<div class=\"mvhdico\"([^>]+)?><span>([^\"<]+)").Groups[2].Value;

                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                {
                    string img = Regex.Match(row, " data-src=\"([^\"]+)\"").Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(img))
                        img = Regex.Match(row, "<img src=\"([^\"]+)\"").Groups[1].Value;

                    string duration = Regex.Match(row, "<span class=\"mbtim\"([^>]+)?>([^<]+)</span>").Groups[2].Value.Trim();

                    var pl = new PlaylistItem()
                    {
                        name = g[2].Value,
                        video = $"{uri}?uri={g[1].Value}",
                        picture = img,
                        quality = quality,
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
                    playlist_url = host + "epr",
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
                            playlist_url = host + "epr"
                        },
                        new MenuItem()
                        {
                            title = "Топ просмотра",
                            playlist_url = host + "epr?sort=most-viewed"
                        },
                        new MenuItem()
                        {
                            title = "Топ рейтинга",
                            playlist_url = host + "epr?sort=top-rated"
                        },
                        new MenuItem()
                        {
                            title = "Длинные ролики",
                            playlist_url = host + "epr?sort=longest"
                        },
                        new MenuItem()
                        {
                            title = "Короткие ролики",
                            playlist_url = host + "epr?sort=shortest"
                        }
                    }
                }
            };
        }

        async public static ValueTask<StreamItem?> StreamLinks(string uri, string host, string? url, Func<string, ValueTask<string?>> onresult, Func<string, ValueTask<string?>> onjson, Func<string, string>? onlog = null)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            string? html = await onresult.Invoke($"{host}/{url}");
            if (html == null)
                return null;

            string vid = Regex.Match(html, "vid ?= ?'([^']+)'").Groups[1].Value;
            string hash = Regex.Match(html, "hash ?= ?'([^']+)'").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(vid) || string.IsNullOrWhiteSpace(hash))
                return null;

            string? json = await onjson.Invoke($"{host}/xhr/video/{vid}?hash={convertHash(hash)}&domain={Regex.Replace(host, "^https?://", "")}&fallback=false&embed=false&supportedFormats=dash,mp4&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
            if (json == null)
                return null;

            onlog?.Invoke("json: " + json);

            var stream_links = new Dictionary<string, string>();
            var match = new Regex("\"src\":( +)?\"(https?://[^/]+/[^\"]+-([0-9]+p).mp4)\",").Match(json);
            while (match.Success)
            {
                onlog?.Invoke($"{match.Groups[3].Value} /  {match.Groups[2].Value}");
                stream_links.TryAdd(match.Groups[3].Value, match.Groups[2].Value);
                match = match.NextMatch();
            }

            onlog?.Invoke("stream_links: " + stream_links.Count);

            return new StreamItem()
            {
                qualitys = stream_links,
                recomends = Playlist(uri, html, pl =>
                {
                    pl.picture = $"{AppInit.rsizehost}/recomends/{pl.picture}";
                    return pl;
                })
            };
        }


        #region convertHash
        static string convertHash(string h)
        {
            return Base36(h.Substring(0, 8)) + Base36(h.Substring(8, 8)) + Base36(h.Substring(16, 8)) + Base36(h.Substring(24, 8));
        }
        #endregion

        #region Base36
        static string Base36(string val)
        {
            string result = "";
            ulong value = Convert.ToUInt64(val, 16);

            const int Base = 36;
            const string Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            while (value > 0)
            {
                result = Chars[(int)(value % Base)] + result; // use StringBuilder for better performance
                value /= Base;
            }

            return result.ToLower();
        }
        #endregion
    }
}

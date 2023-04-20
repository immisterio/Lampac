using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class PornHubTo
    {
        public static ValueTask<string?> InvokeHtml(string host, string? search, string? sort, int pg, Func<string, ValueTask<string?>> onresult)
        {
            string url = $"{host}/";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url += $"video/search?search={HttpUtility.UrlEncode(search)}";
            }
            else
            {
                url += "video";

                if (!string.IsNullOrWhiteSpace(sort))
                    url += $"?o={sort}";
            }

            if (pg > 1)
                url += $"{(url.Contains("?") ? "&" : "?")}page={pg}";

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, string html, Func<PlaylistItem, PlaylistItem>? onplaylist = null)
        {
            var playlists = new List<PlaylistItem>();
            string videoCategory = StringConvert.FindLastText(html, "id=\"videoCategory\"") ??
                                   StringConvert.FindLastText(html, "id=\"content-tv-container\"") ??
                                   StringConvert.FindLastText(html, "id=\"lazyVids\"") ??
                                   StringConvert.FindLastText(html, "id=\"videoSearchResult\"") ?? html;

            foreach (string row in Regex.Split(videoCategory, "(pcVideoListItem |data-video-segment|<li data-id=)").Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row) || row.Contains("premiumIcon") || row.Contains("Porn in русский"))
                    continue;

                string rowfix = Regex.Replace(row, "[\n\r\t]+", "");

                string? m(string pattern, int index = 1)
                {
                    string res = Regex.Match(rowfix, pattern, RegexOptions.IgnoreCase).Groups[index].Value.Trim();
                    if (string.IsNullOrWhiteSpace(res))
                        return null;

                    return res;
                }

                string? vkey = m("(-|_)vkey=\"([^\"]+)\"", 2) ?? m("viewkey=([^\"]+)\"");
                string? title = m("<a href=\"/[^\"]+\" title=\"([^\"]+)\"") ??
                               m("class=\"videoTitle\">([^<]+)<") ??
                               m("href=\"/view_[^\"]+\" onclick=[^>]+>([^<]+)<");

                if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(vkey))
                {
                    string? img = m("(data-mediumthumb|data-path)=\"(https?://[^\"]+)\"", 2) ?? m("<img src=\"([^\"]+)\"");
                    string? duration = m("<var class=\"duration\">([^<]+)</var>") ?? m("class=\"time\">([^<]+)<") ?? m("class=\"videoDuration floatLeft\">([^<]+)<");

                    var pl = new PlaylistItem()
                    {
                        name = title,
                        video = $"{uri}?vkey={vkey}",
                        picture = img ?? string.Empty,
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
            #region getSortName
            string getSortName(string? sort, string emptyName)
            {
                if (string.IsNullOrWhiteSpace(sort))
                    return emptyName;

                switch (sort)
                {
                    case "mr":
                    case "cm":
                        return "новейшее";

                    case "ht":
                        return "самые горячие";

                    case "vi":
                    case "mv":
                        return "больше просмотров";

                    case "ra":
                    case "tr":
                        return "лучшие";

                    default:
                        return emptyName;
                }
            }
            #endregion

            host = string.IsNullOrWhiteSpace(host) ? string.Empty : $"{host}/";

            return new List<MenuItem>()
            {
                new MenuItem()
                {
                    title = "Поиск",
                    search_on = "search_on",
                    playlist_url = host + "phub",
                },
                new MenuItem()
                {
                    title = $"Сортировка: {getSortName(sort, "Недавно в избранном")}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Недавно в избранном",
                            playlist_url = host + "phub"
                        },
                        new MenuItem()
                        {
                            title = "Новейшее",
                            playlist_url = host + "phub?sort=cm"
                        },
                        new MenuItem()
                        {
                            title = "Самые горячие",
                            playlist_url = host + "phub?sort=ht"
                        },
                        new MenuItem()
                        {
                            title = "Лучшие",
                            playlist_url = host + "phub?sort=tr"
                        }
                    }
                }
            };
        }

        async public static ValueTask<Dictionary<string, string>?> StreamLinks(string host, string? vkey, Func<string, ValueTask<string?>> onresult)
        {
            if (string.IsNullOrWhiteSpace(vkey))
                return null;

            string? html = await onresult.Invoke($"{host}/view_video.php?viewkey={vkey}");
            if (html == null)
                return null;

            string? hls = null;
            foreach (string l in getDirectLinks(html))
            {
                if (l.Contains("urlset/master.m3u8"))
                    hls = l;
            }

            if (string.IsNullOrWhiteSpace(hls))
            {
                hls = Regex.Match(html, "\"hls\",\"videoUrl\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                if (string.IsNullOrWhiteSpace(hls))
                    return null;
            }

            return new Dictionary<string, string>()
            {
                ["auto"] = hls.Replace("///", "//")
            };
        }



        #region getDirectLinks
        static List<string> getDirectLinks(string pageCode)
        {
            List<string> vars = new List<string>();
            var getmediaLinks = new List<string>();

            string mainParamBody = Regex.Match(pageCode, "var player_mp4_seek = \"[^\"]+\";[\n\r\t ]+(// var[^\n\r]+[\n\r\t ]+)?([^\n\r]+)").Groups[2].Value.Trim();
            mainParamBody = Regex.Replace(mainParamBody, "/\\*.*?\\*/", "");
            mainParamBody = mainParamBody.Replace("\" + \"", "");


            MatchCollection varMc = Regex.Matches(mainParamBody, "var ([^=]+)=([^;]+);");
            foreach (Match currVar in varMc)
            {
                string name = currVar.Groups[1].Value;
                string param = currVar.Groups[2].Value.Replace("\"", "").Replace(" + ", "");
                vars.Add(name + ";" + param);
            }

            MatchCollection qualMc = Regex.Matches(mainParamBody, "var media_([0-9]+)=(.*?);", RegexOptions.Singleline);
            foreach (Match m in qualMc)
            {
                string link = "";
                string[] parts = m.Groups[2].Value.Replace(" ", "").Split('+');
                foreach (string curr in parts)
                {
                    string? line = vars.Find(x => x.StartsWith(curr));
                    if (line == null)
                        continue;

                    if (line.Split(';').Length > 1)
                    {
                        string? newVal = line.Split(';')[1];
                        link += newVal;
                    }
                }

                getmediaLinks.Add(link);
            }

            return getmediaLinks;
        }
        #endregion
    }
}

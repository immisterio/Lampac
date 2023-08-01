using Lampac.Models.SISI;
using Shared.Model;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class PornHubTo
    {
        public static ValueTask<string?> InvokeHtml(string host, string? search, string? sort, string? hd, int pg, Func<string, ValueTask<string?>> onresult)
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

                if (!string.IsNullOrWhiteSpace(hd))
                    url += (url.Contains("?") ? "&" : "?") + $"hd={hd}";
            }

            if (pg > 1)
                url += $"{(url.Contains("?") ? "&" : "?")}page={pg}";

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, string html, Func<PlaylistItem, PlaylistItem>? onplaylist = null, bool related = false, bool prem = false)
        {
            string? videoCategory = null;
            var playlists = new List<PlaylistItem>() { Capacity = prem ? 50 : 35 };

            if (related)
            {
                var ids = html.Split("id=\"relatedVideosCenter\"");
                if (ids.Length > 1)
                    videoCategory = ids[1];
            }
            else if (html.Contains("id=\"videoCategory\""))
            {
                var ids = html.Split("id=\"videoCategory\"");
                if (ids.Length > 1)
                    videoCategory = ids[1];
            }
            else
            {
                var videorows = Regex.Split(html, "id=\"(content-tv-container|lazyVids|videoSearchResult)\"");
                if (videorows.Length > 2)
                    videoCategory = videorows[2];
            }

            if (videoCategory == null)
                return playlists;

            string splitkey = videoCategory.Contains("pcVideoListItem ") ? "pcVideoListItem " : videoCategory.Contains("data-video-segment") ? "data-video-segment" : "<li data-id=";
            foreach (string row in videoCategory.Split("<h2>Languages</h2>")[0].Split(splitkey).Skip(1))
            {
                string? m(string pattern, int index = 1)
                {
                    string res = Regex.Match(row, pattern).Groups[index].Value;
                    if (string.IsNullOrWhiteSpace(res))
                        return null;

                    return res;
                }

                string? vkey = m("(-|_)vkey=\"([^\"]+)\"", 2) ?? m("viewkey=([^\"]+)\"");
                if (vkey == null)
                    continue;

                string? title = m("href=\"/[^\"]+\" title=\"([^\"]+)\"") ?? m("class=\"videoTitle\">([^<]+)<") ?? m("href=\"/view_[^\"]+\" onclick=[^>]+>([^<]+)<");
                if (title == null)
                    continue;

                string? img = m("data-mediumthumb=\"(https?://[^\"]+)\"") ?? m("data-path=\"(https?://[^\"]+)\"")?.Replace("{index}", "3") ?? m("<img src=\"([^\"]+)\"");
                if (img == null)
                    continue;

                var pl = new PlaylistItem()
                {
                    name = title,
                    video = $"{uri}?vkey={vkey}",
                    picture = img,
                    preview = m("data-mediabook=\"(https?://[^\"]+)\""),
                    time = m("<var class=\"duration\">([^<]+)</var>") ?? m("class=\"time\">([^<]+)<") ?? m("class=\"videoDuration floatLeft\">([^<]+)<"),
                    json = true
                };

                if (onplaylist != null)
                    pl = onplaylist.Invoke(pl);

                playlists.Add(pl);

                if (playlists.Count == (prem ? 48 : 32))
                    break;
            }

            return playlists;
        }

        public static List<MenuItem> Menu(string? host, string? sort, string? hd = null, bool prem = false)
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
            string url = host + (prem ? "pornhubpremium" : "phub");

            var menu = new List<MenuItem>()
            {
                new MenuItem()
                {
                    title = "Поиск",
                    search_on = "search_on",
                    playlist_url = url,
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
                            playlist_url = url + $"?hd={hd}"
                        },
                        new MenuItem()
                        {
                            title = "Новейшее",
                            playlist_url = url + $"?hd={hd}&sort=cm"
                        },
                        new MenuItem()
                        {
                            title = "Самые горячие",
                            playlist_url = url + $"?hd={hd}&sort=ht"
                        },
                        new MenuItem()
                        {
                            title = "Лучшие",
                            playlist_url = url + $"?hd={hd}&sort=tr"
                        }
                    }
                }
            };

            if (prem)
            {
                menu.Add(new MenuItem()
                {
                    title = $"Качество: {(hd == "2" ? "1080p" : hd == "3" ? "1440p" : hd == "4" ? "2160p" : "все")}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Все",
                            playlist_url = url + $"?sort={sort}"
                        },
                        new MenuItem()
                        {
                            title = "2160p",
                            playlist_url = url + $"?sort={sort}&hd=4"
                        },
                        new MenuItem()
                        {
                            title = "1440p",
                            playlist_url = url + $"?sort={sort}&hd=3"
                        },
                        new MenuItem()
                        {
                            title = "1080p",
                            playlist_url = url + $"?sort={sort}&hd=2"
                        }
                    }
                });
            }

            return menu;
        }

        async public static ValueTask<StreamItem?> StreamLinks(string uri, string host, string? vkey, Func<string, ValueTask<string?>> onresult)
        {
            if (string.IsNullOrEmpty(vkey))
                return null;

            string? html = await onresult.Invoke($"{host}/view_video.php?viewkey={vkey}");
            if (html == null)
                return null;

            string? hls = getDirectLinks(html);

            if (string.IsNullOrEmpty(hls))
            {
                hls = Regex.Match(html, "\"hls\",\"videoUrl\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                if (string.IsNullOrEmpty(hls))
                    return null;
            }

            return new StreamItem()
            {
                qualitys = new Dictionary<string, string>()
                {
                    ["auto"] = hls.Replace("///", "//")
                },
                recomends = Playlist(uri, html, related: true, onplaylist: pl => 
                {
                    pl.picture = $"{AppInit.rsizehost}/recomends/{pl.picture}";
                    return pl;
                })
            };
        }



        #region getDirectLinks
        static string? getDirectLinks(string pageCode)
        {
            var vars = new List<(string name, string param)>();

            string mainParamBody = Regex.Match(pageCode, "var player_mp4_seek = \"[^\"]+\";[\n\r\t ]+(// var[^\n\r]+[\n\r\t ]+)?([^\n\r]+)").Groups[2].Value;
            mainParamBody = Regex.Replace(mainParamBody, "/\\*.*?\\*/", "");
            mainParamBody = mainParamBody.Replace("\" + \"", "");

            foreach (Match currVar in Regex.Matches(mainParamBody, "var ([^=]+)=([^;]+);"))
                vars.Add((currVar.Groups[1].Value, currVar.Groups[2].Value.Replace("\"", "").Replace(" + ", "")));

            string mediapattern = /*mainParamBody.Contains("var media_4=") && mainParamBody.Contains("var media_5=") ? "var media_(4)=(.*?);" : */"var media_([0-9]+)=(.*?);";
            foreach (Match m in Regex.Matches(mainParamBody, mediapattern, RegexOptions.Singleline))
            {
                string link = "";
                foreach (string curr in m.Groups[2].Value.Replace(" ", "").Split('+'))
                {
                    string? param = vars.Find(x => x.name == curr).param;
                    if (param == null)
                        continue;

                    link += param;
                }

                if (link.Contains("urlset/master.m3u8"))
                    return link;
            }

            return null;
        }
        #endregion
    }
}

using Shared.Models.SISI.Base;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class PorntrexTo
    {
        public static ValueTask<string> InvokeHtml(string host, string search, string sort, string c, int pg, Func<string, ValueTask<string>> onresult)
        {
            string url = $"{host}/";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url = $"{host}/search/{HttpUtility.UrlEncode(search)}/";

                if (!string.IsNullOrEmpty(sort))
                    url += $"{sort}/";

                url += $"?from_videos={pg}";
            }
            else
            {
                if (!string.IsNullOrEmpty(c))
                {
                    url += $"categories/{c}/";

                    if (sort == "most-popular")
                        url += $"top-rated/";

                    url += $"?from4={pg}";
                }
                else
                {
                    if (string.IsNullOrEmpty(sort))
                    {
                        url += $"latest-updates/{pg}/";
                    }
                    else
                    {
                        url += $"{sort}/weekly/?from4={pg}";
                    }
                }
            }

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, in string html, Func<PlaylistItem, PlaylistItem> onplaylist = null)
        {
            if (string.IsNullOrEmpty(html))
                return new List<PlaylistItem>();

            var rows = html.Split("<div class=\"video-preview-screen");
            var playlists = new List<PlaylistItem>(rows.Length);

            foreach (string row in rows.Skip(1))
            {
                if (row.Contains("<span class=\"line-private\">"))
                    continue;

                var g = Regex.Match(row, "<a href=\"https?://[^/]+/(video/[^\"]+)\" title=\"([^\"]+)\"").Groups;

                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                {
                    string quality = Regex.Match(row, "<span class=\"quality\">([^<]+)</span>").Groups[1].Value;
                    string duration = Regex.Match(row, "<i class=\"fa fa-clock-o\"></i>([^<]+)</div>").Groups[1].Value.Trim();
                    var img = Regex.Match(row, "data-src=\"(https?:)?//(((ptx|statics)\\.cdntrex\\.com/contents/videos_screenshots/[0-9]+/[0-9]+)[^\"]+)").Groups;

                    var pl = new PlaylistItem()
                    {
                        video = $"{uri}?uri={g[1].Value}",
                        name = g[2].Value,
                        picture = $"https://{img[2].Value}",
                        quality = !string.IsNullOrEmpty(quality) ? quality : null,
                        time = duration,
                        json = true,
                        bookmark = new Bookmark()
                        {
                            site = "ptx",
                            href = g[1].Value,
                            image = $"https://{img[2].Value}"
                        }
                    };

                    if (onplaylist != null)
                        pl = onplaylist.Invoke(pl);

                    playlists.Add(pl);
                }
            }

            return playlists;
        }

        public static List<MenuItem> Menu(string host, string search, string sort, string c)
        {
            host = string.IsNullOrWhiteSpace(host) ? string.Empty : $"{host}/";
            string url = host + "ptx";

            if (!string.IsNullOrEmpty(search))
            {
                string encodesearch = HttpUtility.UrlEncode(search);

                return new List<MenuItem>()
                {
                    new MenuItem()
                    {
                        title = "Поиск",
                        search_on = "search_on",
                        playlist_url = url,
                    },
                    new MenuItem()
                    {
                        title = $"Сортировка: {(string.IsNullOrEmpty(sort) ? "Most Relevant" : sort)}",
                        playlist_url = "submenu",
                        submenu = new List<MenuItem>()
                        {
                            new MenuItem()
                            {
                                title = "Most Relevant",
                                playlist_url = url + $"?c={c}&search={encodesearch}"
                            },
                            new MenuItem()
                            {
                                title = "Новинки",
                                playlist_url = url + $"?c={c}&sort=latest-updates&search={encodesearch}"
                            },
                            new MenuItem()
                            {
                                title = "Топ просмотров",
                                playlist_url = url + $"?c={c}&sort=most-popular&search={encodesearch}"
                            }
                        }
                    }
                };
            }

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
                    title = $"Сортировка: {(string.IsNullOrEmpty(sort) ? "новинки" : sort)}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Новинки",
                            playlist_url = url + $"?c={c}"
                        },
                        new MenuItem()
                        {
                            title = "Топ просмотров",
                            playlist_url = url + $"?c={c}&sort=most-popular"
                        }
                    }
                }
            };

            {
                var submenu = new List<MenuItem>()
                {
                    new MenuItem()
                    {
                        title = "Все",
                        playlist_url = url + $"?sort={sort}"
                    },
                    new MenuItem()
                    {
                        title = "4K UHD",
                        playlist_url = url + $"?sort={sort}&c=4k-porn"
                    },
                    new MenuItem()
                    {
                        title = "Anal",
                        playlist_url = url + $"?sort={sort}&c=anal"
                    },
                    new MenuItem()
                    {
                        title = "Arab",
                        playlist_url = url + $"?sort={sort}&c=arab"
                    },
                    new MenuItem()
                    {
                        title = "Asian",
                        playlist_url = url + $"?sort={sort}&c=asian"
                    },
                    new MenuItem()
                    {
                        title = "Ass licking",
                        playlist_url = url + $"?sort={sort}&c=ass-licking"
                    },
                    new MenuItem()
                    {
                        title = "Ass to mouth (ATM)",
                        playlist_url = url + $"?sort={sort}&c=ass-to-mouth"
                    },
                    new MenuItem()
                    {
                        title = "Babe",
                        playlist_url = url + $"?sort={sort}&c=babe"
                    },
                    new MenuItem()
                    {
                        title = "Babysitter",
                        playlist_url = url + $"?sort={sort}&c=babysitter"
                    },
                    new MenuItem()
                    {
                        title = "BBW",
                        playlist_url = url + $"?sort={sort}&c=bbw"
                    },
                    new MenuItem()
                    {
                        title = "Big Ass",
                        playlist_url = url + $"?sort={sort}&c=big-ass"
                    },
                    new MenuItem()
                    {
                        title = "Big Tits",
                        playlist_url = url + $"?sort={sort}&c=big-tits"
                    },
                    new MenuItem()
                    {
                        title = "Black",
                        playlist_url = url + $"?sort={sort}&c=black"
                    },
                    new MenuItem()
                    {
                        title = "Blonde",
                        playlist_url = url + $"?sort={sort}&c=blonde"
                    },
                    new MenuItem()
                    {
                        title = "Blowjob",
                        playlist_url = url + $"?sort={sort}&c=blowjob"
                    },
                    new MenuItem()
                    {
                        title = "Bondage",
                        playlist_url = url + $"?sort={sort}&c=bondage"
                    },
                    new MenuItem()
                    {
                        title = "Brunette",
                        playlist_url = url + $"?sort={sort}&c=brunette"
                    },
                    new MenuItem()
                    {
                        title = "Bukkake",
                        playlist_url = url + $"?sort={sort}&c=bukkake"
                    },
                    new MenuItem()
                    {
                        title = "Busty",
                        playlist_url = url + $"?sort={sort}&c=busty"
                    },
                    new MenuItem()
                    {
                        title = "Casting",
                        playlist_url = url + $"?sort={sort}&c=casting"
                    },
                    new MenuItem()
                    {
                        title = "Celebrities",
                        playlist_url = url + $"?sort={sort}&c=celebrities"
                    },
                    new MenuItem()
                    {
                        title = "College",
                        playlist_url = url + $"?sort={sort}&c=college"
                    },
                    new MenuItem()
                    {
                        title = "Compilation",
                        playlist_url = url + $"?sort={sort}&c=compilation"
                    },
                    new MenuItem()
                    {
                        title = "Creampie",
                        playlist_url = url + $"?sort={sort}&c=creampie"
                    },
                    new MenuItem()
                    {
                        title = "Cuckold",
                        playlist_url = url + $"?sort={sort}&c=cuckold"
                    },
                    new MenuItem()
                    {
                        title = "Cum-swap",
                        playlist_url = url + $"?sort={sort}&c=cum-swapping"
                    },
                    new MenuItem()
                    {
                        title = "Cumshots",
                        playlist_url = url + $"?sort={sort}&c=cumshots"
                    },
                    new MenuItem()
                    {
                        title = "Czech",
                        playlist_url = url + $"?sort={sort}&c=czech"
                    },
                    new MenuItem()
                    {
                        title = "Czech Massage",
                        playlist_url = url + $"?sort={sort}&c=czech-massage"
                    },
                    new MenuItem()
                    {
                        title = "Deepthroat",
                        playlist_url = url + $"?sort={sort}&c=deepthroat"
                    },
                    new MenuItem()
                    {
                        title = "Doggystyle",
                        playlist_url = url + $"?sort={sort}&c=doggystyle"
                    },
                    new MenuItem()
                    {
                        title = "Double Penetration (DP)",
                        playlist_url = url + $"?sort={sort}&c=double-penetration"
                    },
                    new MenuItem()
                    {
                        title = "Ebony",
                        playlist_url = url + $"?sort={sort}&c=ebony"
                    },
                    new MenuItem()
                    {
                        title = "Fantasy",
                        playlist_url = url + $"?sort={sort}&c=fantasy"
                    },
                    new MenuItem()
                    {
                        title = "Fetish",
                        playlist_url = url + $"?sort={sort}&c=fetish"
                    },
                    new MenuItem()
                    {
                        title = "Fingering",
                        playlist_url = url + $"?sort={sort}&c=fingering"
                    },
                    new MenuItem()
                    {
                        title = "Fisting",
                        playlist_url = url + $"?sort={sort}&c=fisting"
                    },
                    new MenuItem()
                    {
                        title = "Footjob",
                        playlist_url = url + $"?sort={sort}&c=footjob"
                    },
                    new MenuItem()
                    {
                        title = "Foursome",
                        playlist_url = url + $"?sort={sort}&c=foursome"
                    },
                    new MenuItem()
                    {
                        title = "Gangbang",
                        playlist_url = url + $"?sort={sort}&c=gangbang"
                    },
                    new MenuItem()
                    {
                        title = "Gangbang Creampie",
                        playlist_url = url + $"?sort={sort}&c=gangbang-creampie"
                    },
                    new MenuItem()
                    {
                        title = "Gaping",
                        playlist_url = url + $"?sort={sort}&c=gaping"
                    },
                    new MenuItem()
                    {
                        title = "Gay",
                        playlist_url = url + $"?sort={sort}&c=gay"
                    },
                    new MenuItem()
                    {
                        title = "German",
                        playlist_url = url + $"?sort={sort}&c=german"
                    },
                    new MenuItem()
                    {
                        title = "Gloryhole",
                        playlist_url = url + $"?sort={sort}&c=gloryhole"
                    },
                    new MenuItem()
                    {
                        title = "Hairy",
                        playlist_url = url + $"?sort={sort}&c=hairy"
                    },
                    new MenuItem()
                    {
                        title = "Handjob",
                        playlist_url = url + $"?sort={sort}&c=handjob"
                    },
                    new MenuItem()
                    {
                        title = "Hardcore",
                        playlist_url = url + $"?sort={sort}&c=hardcore"
                    },
                    new MenuItem()
                    {
                        title = "Hentai",
                        playlist_url = url + $"?sort={sort}&c=hentai"
                    },
                    new MenuItem()
                    {
                        title = "Homemade",
                        playlist_url = url + $"?sort={sort}&c=homemade"
                    },
                    new MenuItem()
                    {
                        title = "Hungarian",
                        playlist_url = url + $"?sort={sort}&c=hungarian"
                    },
                    new MenuItem()
                    {
                        title = "Indian",
                        playlist_url = url + $"?sort={sort}&c=indian"
                    },
                    new MenuItem()
                    {
                        title = "Interracial",
                        playlist_url = url + $"?sort={sort}&c=interracial"
                    },
                    new MenuItem()
                    {
                        title = "Japanese",
                        playlist_url = url + $"?sort={sort}&c=japanese"
                    },
                    new MenuItem()
                    {
                        title = "Latina",
                        playlist_url = url + $"?sort={sort}&c=latina"
                    },
                    new MenuItem()
                    {
                        title = "Lesbian",
                        playlist_url = url + $"?sort={sort}&c=lesbian"
                    },
                    new MenuItem()
                    {
                        title = "Lingerie",
                        playlist_url = url + $"?sort={sort}&c=lingerie"
                    },
                    new MenuItem()
                    {
                        title = "Massage",
                        playlist_url = url + $"?sort={sort}&c=massage"
                    },
                    new MenuItem()
                    {
                        title = "Masturbation",
                        playlist_url = url + $"?sort={sort}&c=masturbation"
                    },
                    new MenuItem()
                    {
                        title = "Mature",
                        playlist_url = url + $"?sort={sort}&c=mature"
                    },
                    new MenuItem()
                    {
                        title = "Milf",
                        playlist_url = url + $"?sort={sort}&c=milf"
                    },
                    new MenuItem()
                    {
                        title = "Office",
                        playlist_url = url + $"?sort={sort}&c=office"
                    },
                    new MenuItem()
                    {
                        title = "Old and Young",
                        playlist_url = url + $"?sort={sort}&c=old-and-young"
                    },
                    new MenuItem()
                    {
                        title = "Orgy",
                        playlist_url = url + $"?sort={sort}&c=orgy"
                    },
                    new MenuItem()
                    {
                        title = "Outdoor",
                        playlist_url = url + $"?sort={sort}&c=outdoor"
                    },
                    new MenuItem()
                    {
                        title = "Petite",
                        playlist_url = url + $"?sort={sort}&c=petite"
                    },
                    new MenuItem()
                    {
                        title = "POV",
                        playlist_url = url + $"?sort={sort}&c=pov"
                    },
                    new MenuItem()
                    {
                        title = "Public",
                        playlist_url = url + $"?sort={sort}&c=public"
                    },
                    new MenuItem()
                    {
                        title = "Pussy licking",
                        playlist_url = url + $"?sort={sort}&c=pussy-licking"
                    },
                    new MenuItem()
                    {
                        title = "Red Head",
                        playlist_url = url + $"?sort={sort}&c=red-head"
                    },
                    new MenuItem()
                    {
                        title = "Riding",
                        playlist_url = url + $"?sort={sort}&c=riding"
                    },
                    new MenuItem()
                    {
                        title = "Russian",
                        playlist_url = url + $"?sort={sort}&c=russian"
                    },
                    new MenuItem()
                    {
                        title = "School Girl",
                        playlist_url = url + $"?sort={sort}&c=school-girl"
                    },
                    new MenuItem()
                    {
                        title = "Shemale",
                        playlist_url = url + $"?sort={sort}&c=shemale"
                    },
                    new MenuItem()
                    {
                        title = "Skinny",
                        playlist_url = url + $"?sort={sort}&c=skinny"
                    },
                    new MenuItem()
                    {
                        title = "Small tits",
                        playlist_url = url + $"?sort={sort}&c=small-tits"
                    },
                    new MenuItem()
                    {
                        title = "Solo",
                        playlist_url = url + $"?sort={sort}&c=solo"
                    },
                    new MenuItem()
                    {
                        title = "Squirt",
                        playlist_url = url + $"?sort={sort}&c=squirt"
                    },
                    new MenuItem()
                    {
                        title = "Strap-on",
                        playlist_url = url + $"?sort={sort}&c=strap-on"
                    },
                    new MenuItem()
                    {
                        title = "Swallow",
                        playlist_url = url + $"?sort={sort}&c=swallow"
                    },
                    new MenuItem()
                    {
                        title = "Teen",
                        playlist_url = url + $"?sort={sort}&c=teen"
                    },
                    new MenuItem()
                    {
                        title = "Threesome",
                        playlist_url = url + $"?sort={sort}&c=threesome"
                    },
                    new MenuItem()
                    {
                        title = "Titfuck",
                        playlist_url = url + $"?sort={sort}&c=titfuck"
                    },
                    new MenuItem()
                    {
                        title = "Toys",
                        playlist_url = url + $"?sort={sort}&c=toys"
                    },
                    new MenuItem()
                    {
                        title = "Uniform",
                        playlist_url = url + $"?sort={sort}&c=uniform"
                    },
                    new MenuItem()
                    {
                        title = "Vintage",
                        playlist_url = url + $"?sort={sort}&c=vintage"
                    },
                    new MenuItem()
                    {
                        title = "Webcam",
                        playlist_url = url + $"?sort={sort}&c=webcam"
                    },
                    new MenuItem()
                    {
                        title = "Wife",
                        playlist_url = url + $"?sort={sort}&c=wife"
                    }
                };

                menu.Add(new MenuItem()
                {
                    title = $"Категория: {submenu.FirstOrDefault(i => i.playlist_url!.EndsWith($"&c={c}")).title ?? "все"}",
                    playlist_url = "submenu",
                    submenu = submenu
                });
            }

            return menu;
        }

        async public static ValueTask<Dictionary<string, string>> StreamLinks(string host, string uri, Func<string, ValueTask<string>> onresult)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return null;

            string html = await onresult.Invoke($"{host}/{uri}");
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

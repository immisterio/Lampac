using Shared.Models.SISI.Base;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class HQpornerTo
    {
        public static ValueTask<string> InvokeHtml(string host, string search, string sort, string c, int pg, Func<string, ValueTask<string>> onresult)
        {
            string url = $"{host}/";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url += $"?q={HttpUtility.UrlEncode(search)}&p={pg}";
            }
            else
            {
                if (!string.IsNullOrEmpty(c))
                {
                    url += $"category/{c}";
                }
                else
                {
                    if (!string.IsNullOrEmpty(sort))
                        url += $"top/{sort}";

                    else
                        url += "hdporn";
                }

                url += $"/{pg}";
            }

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, in string html, Func<PlaylistItem, PlaylistItem> onplaylist = null)
        {
            if (string.IsNullOrEmpty(html))
                return new List<PlaylistItem>();

            var rows = html.Split("<div class=\"img-container\">");
            var playlists = new List<PlaylistItem>(rows.Length);

            foreach (string row in rows.Skip(1))
            {
                var g = Regex.Match(row, "href=\"/([^\"]+)\" class=\"atfi[^\"]+\"><img src=\"//([^\"]+)\"[^>]+ alt=\"([^\"]+)\"").Groups;
                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                {
                    string duration = new Regex("class=\"fa fa-clock-o\" [^>]+></i>([\n\r\t ]+)?([^<]+)<").Match(row).Groups[2].Value.Trim();

                    var pl = new PlaylistItem()
                    {
                        name = g[3].Value.Trim(),
                        video = $"{uri}?uri={g[1].Value}",
                        picture = "https://" + g[2].Value,
                        time = duration,
                        json = true,
                        bookmark = new Bookmark()
                        {
                            site = "hqr",
                            href = g[1].Value,
                            image = "https://" + g[2].Value
                        }
                    };

                    if (onplaylist != null)
                        pl = onplaylist.Invoke(pl);

                    playlists.Add(pl);
                }
            }

            return playlists;
        }

        public static List<MenuItem> Menu(string host, string sort, string c)
        {
            host = string.IsNullOrWhiteSpace(host) ? string.Empty : $"{host}/";
            string url = host + "hqr";

            var menu = new List<MenuItem>()
            {
                new MenuItem()
                {
                    title = "Поиск",
                    search_on = "search_on",
                    playlist_url = url,
                }
            };

            if (string.IsNullOrEmpty(c))
            {
                menu.Add(new MenuItem()
                {
                    title = $"Сортировка: {(string.IsNullOrEmpty(sort) ? "новинки" : sort)}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Самые новые",
                            playlist_url = url + $"?c={c}"
                        },
                        new MenuItem()
                        {
                            title = "Топ недели",
                            playlist_url = url + $"?c={c}&sort=week"
                        },
                        new MenuItem()
                        {
                            title = "Топ месяца",
                            playlist_url = url + $"?c={c}&sort=month"
                        }
                    }
                });
            }

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
                        title = "1080p porn",
                        playlist_url = url + $"?sort={sort}&c=1080p-porn"
                    },
                    new MenuItem()
                    {
                        title = "anal",
                        playlist_url = url + $"?sort={sort}&c=anal-sex-hd"
                    },
                    new MenuItem()
                    {
                        title = "4k porn",
                        playlist_url = url + $"?sort={sort}&c=4k-porn"
                    },
                    new MenuItem()
                    {
                        title = "milf",
                        playlist_url = url + $"?sort={sort}&c=milf"
                    },
                    new MenuItem()
                    {
                        title = "lesbian",
                        playlist_url = url + $"?sort={sort}&c=lesbian"
                    },
                    new MenuItem()
                    {
                        title = "60fps",
                        playlist_url = url + $"?sort={sort}&c=60fps-porn"
                    },
                    new MenuItem()
                    {
                        title = "creampie",
                        playlist_url = url + $"?sort={sort}&c=creampie"
                    },
                    new MenuItem()
                    {
                        title = "big tits",
                        playlist_url = url + $"?sort={sort}&c=big-tits"
                    },
                    new MenuItem()
                    {
                        title = "teen porn",
                        playlist_url = url + $"?sort={sort}&c=teen-porn"
                    },
                    new MenuItem()
                    {
                        title = "pov",
                        playlist_url = url + $"?sort={sort}&c=pov"
                    },
                    new MenuItem()
                    {
                        title = "threesome",
                        playlist_url = url + $"?sort={sort}&c=threesome"
                    },
                    new MenuItem()
                    {
                        title = "asian",
                        playlist_url = url + $"?sort={sort}&c=asian"
                    },
                    new MenuItem()
                    {
                        title = "old and young",
                        playlist_url = url + $"?sort={sort}&c=old-and-young"
                    },
                    new MenuItem()
                    {
                        title = "ebony",
                        playlist_url = url + $"?sort={sort}&c=ebony"
                    },
                    new MenuItem()
                    {
                        title = "big ass",
                        playlist_url = url + $"?sort={sort}&c=big-ass"
                    },
                    new MenuItem()
                    {
                        title = "interracial",
                        playlist_url = url + $"?sort={sort}&c=interracial"
                    },
                    new MenuItem()
                    {
                        title = "squirt",
                        playlist_url = url + $"?sort={sort}&c=squirt"
                    },
                    new MenuItem()
                    {
                        title = "mature",
                        playlist_url = url + $"?sort={sort}&c=mature"
                    },
                    new MenuItem()
                    {
                        title = "sex massage",
                        playlist_url = url + $"?sort={sort}&c=porn-massage"
                    },
                    new MenuItem()
                    {
                        title = "amateur",
                        playlist_url = url + $"?sort={sort}&c=amateur"
                    },
                    new MenuItem()
                    {
                        title = "casting",
                        playlist_url = url + $"?sort={sort}&c=casting"
                    },
                    new MenuItem()
                    {
                        title = "gangbang",
                        playlist_url = url + $"?sort={sort}&c=gangbang"
                    },
                    new MenuItem()
                    {
                        title = "stockings",
                        playlist_url = url + $"?sort={sort}&c=stockings"
                    },
                    new MenuItem()
                    {
                        title = "big dick",
                        playlist_url = url + $"?sort={sort}&c=big-dick"
                    },
                    new MenuItem()
                    {
                        title = "babe",
                        playlist_url = url + $"?sort={sort}&c=babe"
                    },
                    new MenuItem()
                    {
                        title = "latina",
                        playlist_url = url + $"?sort={sort}&c=latina"
                    },
                    new MenuItem()
                    {
                        title = "group sex",
                        playlist_url = url + $"?sort={sort}&c=group-sex"
                    },
                    new MenuItem()
                    {
                        title = "russian",
                        playlist_url = url + $"?sort={sort}&c=russian"
                    },
                    new MenuItem()
                    {
                        title = "masturbation",
                        playlist_url = url + $"?sort={sort}&c=masturbation"
                    },
                    new MenuItem()
                    {
                        title = "hairy pussy",
                        playlist_url = url + $"?sort={sort}&c=hairy-pussy"
                    },
                    new MenuItem()
                    {
                        title = "uniforms",
                        playlist_url = url + $"?sort={sort}&c=uniforms"
                    },
                    new MenuItem()
                    {
                        title = "shemale",
                        playlist_url = url + $"?sort={sort}&c=shemale"
                    },
                    new MenuItem()
                    {
                        title = "blonde",
                        playlist_url = url + $"?sort={sort}&c=blonde"
                    },
                    new MenuItem()
                    {
                        title = "orgasm",
                        playlist_url = url + $"?sort={sort}&c=orgasm"
                    },
                    new MenuItem()
                    {
                        title = "pickup",
                        playlist_url = url + $"?sort={sort}&c=pickup"
                    },
                    new MenuItem()
                    {
                        title = "sex party",
                        playlist_url = url + $"?sort={sort}&c=sex-parties"
                    },
                    new MenuItem()
                    {
                        title = "bdsm",
                        playlist_url = url + $"?sort={sort}&c=bdsm"
                    },
                    new MenuItem()
                    {
                        title = "public",
                        playlist_url = url + $"?sort={sort}&c=public"
                    },
                    new MenuItem()
                    {
                        title = "japanese",
                        playlist_url = url + $"?sort={sort}&c=japanese-girls-porn"
                    },
                    new MenuItem()
                    {
                        title = "redhead",
                        playlist_url = url + $"?sort={sort}&c=redhead"
                    },
                    new MenuItem()
                    {
                        title = "orgy",
                        playlist_url = url + $"?sort={sort}&c=orgy"
                    },
                    new MenuItem()
                    {
                        title = "blowjob",
                        playlist_url = url + $"?sort={sort}&c=blowjob"
                    },
                    new MenuItem()
                    {
                        title = "fetish",
                        playlist_url = url + $"?sort={sort}&c=fetish"
                    },
                    new MenuItem()
                    {
                        title = "brunette",
                        playlist_url = url + $"?sort={sort}&c=brunette"
                    },
                    new MenuItem()
                    {
                        title = "small tits",
                        playlist_url = url + $"?sort={sort}&c=small-tits"
                    },
                    new MenuItem()
                    {
                        title = "undressing",
                        playlist_url = url + $"?sort={sort}&c=undressing"
                    },
                    new MenuItem()
                    {
                        title = "cumshot",
                        playlist_url = url + $"?sort={sort}&c=cumshot"
                    },
                    new MenuItem()
                    {
                        title = "outdoor",
                        playlist_url = url + $"?sort={sort}&c=outdoor"
                    },
                    new MenuItem()
                    {
                        title = "deepthroat",
                        playlist_url = url + $"?sort={sort}&c=deepthroat"
                    },
                    new MenuItem()
                    {
                        title = "bondage",
                        playlist_url = url + $"?sort={sort}&c=bondage"
                    },
                    new MenuItem()
                    {
                        title = "shaved pussy",
                        playlist_url = url + $"?sort={sort}&c=shaved-pussy"
                    },
                    new MenuItem()
                    {
                        title = "bisexual",
                        playlist_url = url + $"?sort={sort}&c=bisexual"
                    },
                    new MenuItem()
                    {
                        title = "hentai",
                        playlist_url = url + $"?sort={sort}&c=hentai"
                    },
                    new MenuItem()
                    {
                        title = "handjob",
                        playlist_url = url + $"?sort={sort}&c=handjob"
                    },
                    new MenuItem()
                    {
                        title = "pussy licking",
                        playlist_url = url + $"?sort={sort}&c=pussy-licking"
                    },
                    new MenuItem()
                    {
                        title = "moaning",
                        playlist_url = url + $"?sort={sort}&c=moaning"
                    },
                    new MenuItem()
                    {
                        title = "fisting",
                        playlist_url = url + $"?sort={sort}&c=fisting"
                    },
                    new MenuItem()
                    {
                        title = "vintage",
                        playlist_url = url + $"?sort={sort}&c=vintage"
                    },
                    new MenuItem()
                    {
                        title = "tattooed",
                        playlist_url = url + $"?sort={sort}&c=tattooed"
                    },
                    new MenuItem()
                    {
                        title = "beach",
                        playlist_url = url + $"?sort={sort}&c=beach-porn"
                    },
                    new MenuItem()
                    {
                        title = "vibrator",
                        playlist_url = url + $"?sort={sort}&c=vibrator"
                    },
                    new MenuItem()
                    {
                        title = "fingering",
                        playlist_url = url + $"?sort={sort}&c=fingering"
                    },
                    new MenuItem()
                    {
                        title = "squeezing tits",
                        playlist_url = url + $"?sort={sort}&c=squeezing-tits"
                    },
                    new MenuItem()
                    {
                        title = "long hair",
                        playlist_url = url + $"?sort={sort}&c=long-hair"
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

        async public static ValueTask<Dictionary<string, string>> StreamLinks(string host, string uri, Func<string, ValueTask<string>> onresult, Func<string, ValueTask<string>> oniframe)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return null;

            string html = await onresult.Invoke($"{host}/{uri}");
            if (html == null)
                return null;

            string uriframe = Regex.Match(html, "<iframe src=\"//([^/]+/video/[^/]+/)\"").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(uriframe))
                return null;

            string iframeHtml = await oniframe.Invoke($"https://{uriframe}");
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

            if (stream_links.Count == 0)
            {
                string jw = Regex.Match(iframeHtml, "\\$\\(\"#jw\"\\)([^;]+)").Groups[1].Value;
                if (jw.Contains("replaceAll"))
                {
                    var grpal = Regex.Match(iframeHtml, "replaceAll\\(\"([^\"]+)\",([^\\+]+)\\+\"pubs/\"\\+([^\\+]+)").Groups;

                    string cdn = Regex.Match(iframeHtml, grpal[2].Value + "=\"([^\"]+)\"").Groups[1].Value;
                    string hash = Regex.Match(iframeHtml, grpal[3].Value + "=\"([^\"]+)\"").Groups[1].Value;

                    if (!string.IsNullOrEmpty(cdn) && !string.IsNullOrEmpty(hash))
                    {
                        match = new Regex("src=\"([^\"]+[0-9]+\\.mp4)\" title=\"([^\"]+)\"").Match(iframeHtml.Replace("\\", ""));
                        while (match.Success)
                        {
                            if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[2].Value) && !match.Groups[2].Value.Contains("Default"))
                            {
                                string hls = match.Groups[1].Value.Replace(grpal[1].Value, $"https:{cdn}pubs/{hash}/");

                                if (hls.StartsWith("https:"))
                                    stream_links.TryAdd(match.Groups[2].Value, hls);
                            }

                            match = match.NextMatch();
                        }
                    }
                }
            }

            return stream_links.Reverse().ToDictionary(k => k.Key, v => v.Value);
        }
    }
}

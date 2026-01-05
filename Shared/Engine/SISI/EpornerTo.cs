using HtmlAgilityPack;
using Shared.Engine.RxEnumerate;
using Shared.Models.SISI.Base;
using Shared.Models.SISI.OnResult;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class EpornerTo
    {
        static readonly ThreadLocal<StringBuilder> sb = new(() => new StringBuilder(64));


        public static Task<string> InvokeHtml(string host, string search, string sort, string c, int pg, Func<string, Task<string>> onresult)
        {
            string url = $"{host}/";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url += $"search/{HttpUtility.UrlEncode(search)}/";

                if (pg > 1)
                    url += $"{pg}/";

                if (!string.IsNullOrEmpty(sort))
                    url += $"{sort}/";
            }
            else
            {
                if (!string.IsNullOrEmpty(c)) 
                {
                    url += $"cat/{c}/";

                    if (pg > 1)
                        url += $"{pg}/";
                }
                else
                {
                    if (pg > 1)
                        url += $"{pg}/";

                    if (!string.IsNullOrEmpty(sort))
                        url += $"{sort}/";
                }
            }

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, string html, Func<PlaylistItem, PlaylistItem> onplaylist = null)
        {
            if (string.IsNullOrEmpty(html))
                return new List<PlaylistItem>();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            string single = doc.DocumentNode.SelectSingleNode("//*[@id='relateddiv' or @id='vidresults']")?.InnerHtml;
            if (single != null)
                html = single;
            else
            {
                if (html.Contains("class=\"toptopbelinset\""))
                    html = html.Split("class=\"toptopbelinset\"")[1];

                if (html.Contains("class=\"relatedtext\""))
                    html = html.Split("class=\"relatedtext\"")[1];
            }

            var rx = Rx.Split("<div class=\"mb( hdy)?\"", html, 1);

            var playlists = new List<PlaylistItem>(rx.Count);

            foreach (var row in rx.Rows())
            {
                var g = row.Groups("<p class=\"mbtit\"><a href=\"/([^\"]+)\">([^<]+)</a>");
                string quality = row.Match("<div class=\"mvhdico\"([^>]+)?><span>([^\"<]+)", 2);

                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                {
                    string img = row.Match(" data-src=\"([^\"]+)\"");
                    if (string.IsNullOrWhiteSpace(img))
                        img = row.Match("<img src=\"([^\"]+)\"");

                    string dataid = row.Match("data-id=\"([^\"]+)\"");
                    string preview = Regex.Replace(img, "/[^/]+$", "") + $"/{dataid}-preview.webm";

                    string duration = row.Match("<span class=\"mbtim\"([^>]+)?>([^<]+)</span>", 2, trim: true);

                    var pl = new PlaylistItem()
                    {
                        name = g[2].Value,
                        video = $"{uri}?uri={g[1].Value}",
                        picture = img,
                        preview = preview,
                        quality = quality,
                        time = duration,
                        json = true,
                        related = true,
                        bookmark = new Bookmark()
                        {
                            site = "epr",
                            href = g[1].Value,
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

        public static List<MenuItem> Menu(string host, string search, string sort, string c)
        {
            host = string.IsNullOrWhiteSpace(host) ? string.Empty : $"{host}/";
            string url = host + "epr";

            var menu = new List<MenuItem>()
            {
                new MenuItem()
                {
                    title = "Поиск",
                    search_on = "search_on",
                    playlist_url = url,
                }
            };

            if (!string.IsNullOrEmpty(search))
            {
                string encodesearch = HttpUtility.UrlEncode(search);

                menu.Add(new MenuItem()
                {
                    title = $"Сортировка: {(string.IsNullOrWhiteSpace(sort) ? "новинки" : sort)}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Новинки",
                            playlist_url = url + $"?search={encodesearch}"
                        },
                        new MenuItem()
                        {
                            title = "Топ просмотра",
                            playlist_url = url + $"?sort=most-viewed&search={encodesearch}"
                        },
                        new MenuItem()
                        {
                            title = "Топ рейтинга",
                            playlist_url = url + $"?sort=top-rated&search={encodesearch}"
                        },
                        new MenuItem()
                        {
                            title = "Длинные ролики",
                            playlist_url = url + $"?sort=longest&search={encodesearch}"
                        },
                        new MenuItem()
                        {
                            title = "Короткие ролики",
                            playlist_url = url + $"?sort=shortest&search={encodesearch}"
                        }
                    }
                });

                return menu;
            }

            if (string.IsNullOrEmpty(c))
            {
                menu.Add(new MenuItem()
                {
                    title = $"Сортировка: {(string.IsNullOrWhiteSpace(sort) ? "новинки" : sort)}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Новинки",
                            playlist_url = url
                        },
                        new MenuItem()
                        {
                            title = "Топ просмотра",
                            playlist_url = url + "?sort=most-viewed"
                        },
                        new MenuItem()
                        {
                            title = "Топ рейтинга",
                            playlist_url = url + "?sort=top-rated"
                        },
                        new MenuItem()
                        {
                            title = "Длинные ролики",
                            playlist_url = url + "?sort=longest"
                        },
                        new MenuItem()
                        {
                            title = "Короткие ролики",
                            playlist_url = url + "?sort=shortest"
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
                        playlist_url = url
                    },
                    new MenuItem()
                    {
                        title = "4K UHD",
                        playlist_url = url + $"?c=4k-porn"
                    },
                    new MenuItem()
                    {
                        title = "60 FPS",
                        playlist_url = url + $"?c=60fps"
                    },
                    new MenuItem()
                    {
                        title = "Amateur",
                        playlist_url = url + $"?c=amateur"
                    },
                    new MenuItem()
                    {
                        title = "Anal",
                        playlist_url = url + $"?c=anal"
                    },
                    new MenuItem()
                    {
                        title = "Asian",
                        playlist_url = url + $"?c=asian"
                    },
                    new MenuItem()
                    {
                        title = "ASMR",
                        playlist_url = url + $"?c=asmr"
                    },
                    new MenuItem()
                    {
                        title = "BBW",
                        playlist_url = url + $"?c=bbw"
                    },
                    new MenuItem()
                    {
                        title = "BDSM",
                        playlist_url = url + $"?c=bdsm"
                    },
                    new MenuItem()
                    {
                        title = "Big Ass",
                        playlist_url = url + $"?c=big-ass"
                    },
                    new MenuItem()
                    {
                        title = "Big Dick",
                        playlist_url = url + $"?c=big-dick"
                    },
                    new MenuItem()
                    {
                        title = "Big Tits",
                        playlist_url = url + $"?c=big-tits"
                    },
                    new MenuItem()
                    {
                        title = "Bisexual",
                        playlist_url = url + $"?c=bisexual"
                    },
                    new MenuItem()
                    {
                        title = "Blonde",
                        playlist_url = url + $"?c=blonde"
                    },
                    new MenuItem()
                    {
                        title = "Blowjob",
                        playlist_url = url + $"?c=blowjob"
                    },
                    new MenuItem()
                    {
                        title = "Bondage",
                        playlist_url = url + $"?c=bondage"
                    },
                    new MenuItem()
                    {
                        title = "Brunette",
                        playlist_url = url + $"?c=brunette"
                    },
                    new MenuItem()
                    {
                        title = "Bukkake",
                        playlist_url = url + $"?c=bukkake"
                    },
                    new MenuItem()
                    {
                        title = "Creampie",
                        playlist_url = url + $"?c=creampie"
                    },
                    new MenuItem()
                    {
                        title = "Cumshot",
                        playlist_url = url + $"?c=cumshot"
                    },
                    new MenuItem()
                    {
                        title = "Double Penetration",
                        playlist_url = url + $"?c=double-penetration"
                    },
                    new MenuItem()
                    {
                        title = "Ebony",
                        playlist_url = url + $"?c=ebony"
                    },
                    new MenuItem()
                    {
                        title = "Fat",
                        playlist_url = url + $"?c=fat"
                    },
                    new MenuItem()
                    {
                        title = "Fetish",
                        playlist_url = url + $"?c=fetish"
                    },
                    new MenuItem()
                    {
                        title = "Fisting",
                        playlist_url = url + $"?c=fisting"
                    },
                    new MenuItem()
                    {
                        title = "Footjob",
                        playlist_url = url + $"?c=footjob"
                    },
                    new MenuItem()
                    {
                        title = "For Women",
                        playlist_url = url + $"?c=for-women"
                    },
                    new MenuItem()
                    {
                        title = "Gay",
                        playlist_url = url + $"?c=gay"
                    },
                    new MenuItem()
                    {
                        title = "Group Sex",
                        playlist_url = url + $"?c=group-sex"
                    },
                    new MenuItem()
                    {
                        title = "Handjob",
                        playlist_url = url + $"?c=handjob"
                    },
                    new MenuItem()
                    {
                        title = "Hardcore",
                        playlist_url = url + $"?c=hardcore"
                    },
                    new MenuItem()
                    {
                        title = "Hentai",
                        playlist_url = url + $"?c=hentai"
                    },
                    new MenuItem()
                    {
                        title = "Homemade",
                        playlist_url = url + $"?c=homemade"
                    },
                    new MenuItem()
                    {
                        title = "Hotel",
                        playlist_url = url + $"?c=hotel"
                    },
                    new MenuItem()
                    {
                        title = "Housewives",
                        playlist_url = url + $"?c=housewives"
                    },
                    new MenuItem()
                    {
                        title = "Indian",
                        playlist_url = url + $"?c=indian"
                    },
                    new MenuItem()
                    {
                        title = "Interracial",
                        playlist_url = url + $"?c=interracial"
                    },
                    new MenuItem()
                    {
                        title = "Japanese",
                        playlist_url = url + $"?c=japanese"
                    },
                    new MenuItem()
                    {
                        title = "Latina",
                        playlist_url = url + $"?c=latina"
                    },
                    new MenuItem()
                    {
                        title = "Lesbian",
                        playlist_url = url + $"?c=lesbians"
                    },
                    new MenuItem()
                    {
                        title = "Lingerie",
                        playlist_url = url + $"?c=lingerie"
                    },
                    new MenuItem()
                    {
                        title = "Massage",
                        playlist_url = url + $"?c=massage"
                    },
                    new MenuItem()
                    {
                        title = "Masturbation",
                        playlist_url = url + $"?c=masturbation"
                    },
                    new MenuItem()
                    {
                        title = "Mature",
                        playlist_url = url + $"?c=mature"
                    },
                    new MenuItem()
                    {
                        title = "MILF",
                        playlist_url = url + $"?c=milf"
                    },
                    new MenuItem()
                    {
                        title = "Nurses",
                        playlist_url = url + $"?c=nurse"
                    },
                    new MenuItem()
                    {
                        title = "Office",
                        playlist_url = url + $"?c=office"
                    },
                    new MenuItem()
                    {
                        title = "Older Men",
                        playlist_url = url + $"?c=old-man"
                    },
                    new MenuItem()
                    {
                        title = "Orgy",
                        playlist_url = url + $"?c=orgy"
                    },
                    new MenuItem()
                    {
                        title = "Outdoor",
                        playlist_url = url + $"?c=outdoor"
                    },
                    new MenuItem()
                    {
                        title = "Petite",
                        playlist_url = url + $"?c=petite"
                    },
                    new MenuItem()
                    {
                        title = "Pornstar",
                        playlist_url = url + $"?c=pornstar"
                    },
                    new MenuItem()
                    {
                        title = "POV",
                        playlist_url = url + $"?c=pov-porn"
                    },
                    new MenuItem()
                    {
                        title = "Public",
                        playlist_url = url + $"?c=public"
                    },
                    new MenuItem()
                    {
                        title = "Redhead",
                        playlist_url = url + $"?c=redhead"
                    },
                    new MenuItem()
                    {
                        title = "Shemale",
                        playlist_url = url + $"?c=shemale"
                    },
                    new MenuItem()
                    {
                        title = "Sleep",
                        playlist_url = url + $"?c=sleep"
                    },
                    new MenuItem()
                    {
                        title = "Small Tits",
                        playlist_url = url + $"?c=small-tits"
                    },
                    new MenuItem()
                    {
                        title = "Squirt",
                        playlist_url = url + $"?c=squirt"
                    },
                    new MenuItem()
                    {
                        title = "Striptease",
                        playlist_url = url + $"?c=striptease"
                    },
                    new MenuItem()
                    {
                        title = "Students",
                        playlist_url = url + $"?c=students"
                    },
                    new MenuItem()
                    {
                        title = "Swinger",
                        playlist_url = url + $"?c=swingers"
                    },
                    new MenuItem()
                    {
                        title = "Teen",
                        playlist_url = url + $"?c=teens"
                    },
                    new MenuItem()
                    {
                        title = "Threesome",
                        playlist_url = url + $"?c=threesome"
                    },
                    new MenuItem()
                    {
                        title = "Toys",
                        playlist_url = url + $"?c=toys"
                    },
                    new MenuItem()
                    {
                        title = "Uncategorized",
                        playlist_url = url + $"?c=uncategorized"
                    },
                    new MenuItem()
                    {
                        title = "Uniform",
                        playlist_url = url + $"?c=uniform"
                    },
                    new MenuItem()
                    {
                        title = "Vintage",
                        playlist_url = url + $"?c=vintage"
                    },
                    new MenuItem()
                    {
                        title = "Webcam",
                        playlist_url = url + $"?c=webcam"
                    }
                };

                menu.Add(new MenuItem()
                {
                    title = $"Категория: {submenu.FirstOrDefault(i => i.playlist_url!.EndsWith($"c={c}")).title ?? "все"}",
                    playlist_url = "submenu",
                    submenu = submenu
                });
            }

            return menu;
        }

        async public static Task<StreamItem> StreamLinks(string uri, string host, string url, Func<string, Task<string>> onresult, Func<string, Task<string>> onjson, Func<string, string> onlog = null)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            string html = await onresult.Invoke($"{host}/{url}");
            if (html == null)
                return null;

            string vid = Regex.Match(html, "vid ?= ?'([^']+)'").Groups[1].Value;
            string hash = Regex.Match(html, "hash ?= ?'([^']+)'").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(vid) || string.IsNullOrWhiteSpace(hash))
                return null;

            string json = await onjson.Invoke($"{host}/xhr/video/{vid}?hash={convertHash(hash)}&domain={Regex.Replace(host, "^https?://", "")}&fallback=false&embed=false&supportedFormats=dash,mp4&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
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
                recomends = Playlist(uri, html)
            };
        }


        #region convertHash
        static string convertHash(string h)
        {
            StringBuilder builder = sb.Value;
            builder.Clear();

            Base36(h.AsSpan(0, 8), builder);
            Base36(h.AsSpan(8, 8), builder);
            Base36(h.AsSpan(16, 8), builder);
            Base36(h.AsSpan(24, 8), builder);

            return builder.ToString();
        }
        #endregion

        #region Base36
        static void Base36(ReadOnlySpan<char> hex, StringBuilder builder)
        {
            // Парсинг 8 hex-символов без Substring и без аллокаций
            ulong value = ulong.Parse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);

            const int Base = 36;
            const string Chars = "0123456789abcdefghijklmnopqrstuvwxyz";

            int start = builder.Length;

            if (value == 0)
            {
                builder.Append('0');
                return;
            }

            while (value > 0)
            {
                builder.Append(Chars[(int)(value % Base)]);
                value /= Base;
            }

            // Разворот добавленного участка (т.к. цифры добавлялись в обратном порядке)
            int i = start;
            int j = builder.Length - 1;
            while (i < j)
            {
                (builder[i], builder[j]) = (builder[j], builder[i]);
                i++;
                j--;
            }
        }
        #endregion
    }
}

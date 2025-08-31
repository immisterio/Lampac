using HtmlAgilityPack;
using Shared.Models.SISI.Base;
using Shared.Models.SISI.OnResult;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class PornHubTo
    {
        public static ValueTask<string> InvokeHtml(string host, string plugin, string search, string model, string sort, int c, string hd, int pg, Func<string, ValueTask<string>> onresult)
        {
            string url = $"{host}/";

            if (!string.IsNullOrEmpty(search))
            {
                url += $"video/search?search={HttpUtility.UrlEncode(search)}";

                if (!string.IsNullOrEmpty(sort))
                    url += $"&o={sort}";
            }
            else if (!string.IsNullOrEmpty(model))
            {
                if (model.StartsWith("pornstar/"))
                    url += $"{model}/videos/upload";
                else
                    url += $"model/{model}/videos";
            }
            else
            {
                switch (plugin ?? "")
                {
                    case "phubgay":
                        url += "gay/video";
                        break;
                    case "phubsml":
                        url += "transgender";
                        break;
                    default:
                        url += "video";
                        break;
                }

                if (!string.IsNullOrEmpty(sort))
                    url += $"?o={sort}";

                if (!string.IsNullOrEmpty(hd))
                    url += (url.Contains("?") ? "&" : "?") + $"hd={hd}";

                if (c > 0)
                    url += (url.Contains("?") ? "&" : "?") + $"c={c}";
            }

            if (pg > 1)
                url += $"{(url.Contains("?") ? "&" : "?")}page={pg}";

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string video_uri, string list_uri, in string html, Func<PlaylistItem, PlaylistItem> onplaylist = null, bool related = false, bool prem = false, bool IsModel_page = false)
        {
            if (string.IsNullOrEmpty(html))
                return new List<PlaylistItem>();

            string videoCategory = null;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var node = doc.DocumentNode;

            if (related)
            {
                videoCategory = node.SelectSingleNode("//*[@id='relatedVideosListing' or @id='relatedVideos']")?.InnerHtml;
            }
            else if (html.Contains("id=\"videoCategory\""))
            {
                videoCategory = node.SelectSingleNode("//*[@id='videoCategory']")?.InnerHtml;
            }
            else if (html.Contains("videoList clearfix browseVideo-tabSplit"))
            {
                var ids = html.Split("videoList clearfix browseVideo-tabSplit");
                if (ids.Length > 1)
                    videoCategory = ids[1].Split("<h2>Languages</h2>")[0].Split("pageHeader")[0];
            }
            else
            {
                videoCategory = node.SelectSingleNode("//*[@id='videoSearchResult' or @id='mostRecentVideosSection' or @id='moreData' or @id='content-tv-container' or @id='lazyVids']")?.InnerHtml;
            }

            if (videoCategory == null)
                return new List<PlaylistItem>();

            ModelItem model = null;
            if (IsModel_page) 
            {
                string name = Regex.Match(html, "itemprop=\"name\">([\r\n\t ]+)?([^<]+)</h1>").Groups[2].Value.Trim();
                string href = Regex.Match(html, "rel=\"canonical\" href=\"(https?://[^/]+)?/model/([^/]+)/").Groups[2].Value;

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(href))
                {
                    model = new ModelItem()
                    {
                        name = name,
                        uri = list_uri + (list_uri.Contains("?") ? "&" : "?") + $"model={href}",
                    };
                }
            }

            string splitkey = videoCategory.Contains("pcVideoListItem ") ? "pcVideoListItem " : videoCategory.Contains("data-video-segment") ? "data-video-segment" : videoCategory.Contains("<li data-id=") ? "<li data-id=" : "<li id=";

            var rows = videoCategory.Split(splitkey);
            var playlists = new List<PlaylistItem>(rows.Length);

            foreach (string row in rows.Skip(1))
            {
                if (row.Contains("brand__badge") || row.Contains("private-vid-title"))
                    continue;

                string m(string pattern, int index = 1)
                {
                    string res = Regex.Match(row, pattern).Groups[index].Value;
                    if (string.IsNullOrWhiteSpace(res))
                        return null;

                    return res;
                }

                string vkey = m("(-|_)vkey=\"([^\"]+)\"", 2) ?? m("viewkey=([^\"]+)\"");
                if (vkey == null)
                    continue;

                string title = m("href=\"/[^\"]+\" title=\"([^\"]+)\"") ?? m("class=\"videoTitle\">([^<]+)<") ?? m("href=\"/view_[^\"]+\" onclick=[^>]+>([^<]+)<");
                if (title == null)
                    continue;

                string img = m("data-mediumthumb=\"(https?://[^\"]+)\"") ?? m("data-path=\"(https?://[^\"]+)\"")?.Replace("{index}", "3") ?? m("<img src=\"([^\"]+)\"");
                if (img == null)
                    continue;

                if (!IsModel_page)
                {
                    model = null;
                    var gmodel = Regex.Match(row, "href=\"/model/([^\"]+)\"[^>]+>([^<]+)<");
                    if (string.IsNullOrEmpty(gmodel.Groups[1].Value))
                        gmodel = Regex.Match(row, "href=\"/(pornstar/[^\"]+)\"[^>]+>([^<]+)<");

                    if (!string.IsNullOrEmpty(gmodel.Groups[1].Value))
                    {
                        model = new ModelItem()
                        {
                            name = gmodel.Groups[2].Value,
                            uri = list_uri + (list_uri.Contains("?") ? "&" : "?") + $"model={gmodel.Groups[1].Value}",
                        };
                    }
                }

                var pl = new PlaylistItem()
                {
                    name = title,
                    video = $"{video_uri}?vkey={vkey}",
                    model = model,
                    picture = img,
                    preview = m("data-mediabook=\"(https?://[^\"]+)\"") ?? m("data-webm=\"(https?://[^\"]+)\""),
                    time = m("<var class=\"duration\">([^<]+)</var>") ?? m("class=\"time\">([^<]+)<") ?? m("class=\"videoDuration floatLeft\">([^<]+)<") ?? m("time\">([^<]+)<"),
                    json = true,
                    related = true,
                    bookmark = new Bookmark()
                    {
                        site = prem ? "phubprem" : "phub",
                        href = vkey,
                        image = img
                    }
                };

                if (onplaylist != null)
                    pl = onplaylist.Invoke(pl);

                playlists.Add(pl);
            }

            return playlists;
        }

        public static List<MenuItem> Menu(string host, string plugin, string search, string sort, int c, string hd = null)
        {
            #region getSortName
            string getSortName(string sort, string emptyName)
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
            string url = host + plugin;

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
                        title = $"Сортировка: {getSortName(sort, "Наиболее актуальное")}",
                        playlist_url = "submenu",
                        submenu = new List<MenuItem>()
                        {
                            new MenuItem()
                            {
                                title = "Наиболее актуальное",
                                playlist_url = url + $"?search={encodesearch}"
                            },
                            new MenuItem()
                            {
                                title = "Новейшее",
                                playlist_url = url + $"?search={encodesearch}&sort=mr"
                            },
                            new MenuItem()
                            {
                                title = "Лучшие",
                                playlist_url = url + $"?search={encodesearch}&sort=tr"
                            },
                            new MenuItem()
                            {
                                title = "Больше просмотров",
                                playlist_url = url + $"?search={encodesearch}&sort=mv"
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
                    title = $"Сортировка: {getSortName(sort, "Недавно в избранном")}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Недавно в избранном",
                            playlist_url = url + $"?hd={hd}&c={c}"
                        },
                        new MenuItem()
                        {
                            title = "Новейшее",
                            playlist_url = url + $"?hd={hd}&c={c}&sort=cm"
                        },
                        new MenuItem()
                        {
                            title = "Самые горячие",
                            playlist_url = url + $"?hd={hd}&c={c}&sort=ht"
                        },
                        new MenuItem()
                        {
                            title = "Лучшие",
                            playlist_url = url + $"?hd={hd}&c={c}&sort=tr"
                        }
                    }
                }
            };

            if (plugin == "pornhubpremium" || plugin == "phubprem")
            {
                menu.Insert(1, new MenuItem()
                {
                    title = $"Качество: {(hd == "2" ? "1080p" : hd == "3" ? "1440p" : hd == "4" ? "2160p" : "все")}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Все",
                            playlist_url = url + $"?sort={sort}&c={c}"
                        },
                        new MenuItem()
                        {
                            title = "2160p",
                            playlist_url = url + $"?sort={sort}&c={c}&hd=4"
                        },
                        new MenuItem()
                        {
                            title = "1440p",
                            playlist_url = url + $"?sort={sort}&c={c}&hd=3"
                        },
                        new MenuItem()
                        {
                            title = "1080p",
                            playlist_url = url + $"?sort={sort}&c={c}&hd=2"
                        }
                    }
                });
            }
            else
            {
                menu.Add(new MenuItem()
                {
                    title = $"Ориентация: {(plugin == "phubgay" ? "Геи" : plugin == "phubsml" ? "Трансы" : "Гетеро")}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Гетеро",
                            playlist_url = host + "phub",
                        },
                        new MenuItem()
                        {
                            title = "Геи",
                            playlist_url = host + "phubgay",
                        },
                        new MenuItem()
                        {
                            title = "Трансы",
                            playlist_url = host + "phubsml",
                        }
                    }
                });
            }

            if (plugin == "phubgay")
            {
                var submenu = new List<MenuItem>()
                {
                    new MenuItem()
                    {
                        title = "Все",
                        playlist_url = url + $"?hd={hd}&sort={sort}"
                    },
                    new MenuItem()
                    {
                        title = "Азиаты",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=48"
                    },
                    new MenuItem()
                    {
                        title = "Без презерватива",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=40"
                    },
                    new MenuItem()
                    {
                        title = "Большие члены",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=58"
                    },
                    new MenuItem()
                    {
                        title = "Веб-камера",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=342"
                    },
                    new MenuItem()
                    {
                        title = "Гонзо",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=372"
                    },
                    new MenuItem()
                    {
                        title = "Грубый секс",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=312"
                    },
                    new MenuItem()
                    {
                        title = "Дрочит",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=262"
                    },
                    new MenuItem()
                    {
                        title = "Жеребцы",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=70"
                    },
                    new MenuItem()
                    {
                        title = "Зрелые",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=332"
                    },
                    new MenuItem()
                    {
                        title = "Кастинги",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=362"
                    },
                    new MenuItem()
                    {
                        title = "Качки",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=322"
                    },
                    new MenuItem()
                    {
                        title = "Колледж",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=68"
                    },
                    new MenuItem()
                    {
                        title = "Кончают",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=352"
                    },
                    new MenuItem()
                    {
                        title = "Кремпай",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=71"
                    },
                    new MenuItem()
                    {
                        title = "Латино",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=50"
                    },
                    new MenuItem()
                    {
                        title = "Любительское",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=252"
                    },
                    new MenuItem()
                    {
                        title = "Массаж",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=45"
                    },
                    new MenuItem()
                    {
                        title = "Медведь",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=66"
                    },
                    new MenuItem()
                    {
                        title = "Межрассовый Секс",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=64"
                    },
                    new MenuItem()
                    {
                        title = "Минет",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=56"
                    },
                    new MenuItem()
                    {
                        title = "Молоденькие геи",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=49"
                    },
                    new MenuItem()
                    {
                        title = "Мультики",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=422"
                    },
                    new MenuItem()
                    {
                        title = "Мускулистые",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=51"
                    },
                    new MenuItem()
                    {
                        title = "На публике",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=84"
                    },
                    new MenuItem()
                    {
                        title = "Не обрезанные",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=272"
                    },
                    new MenuItem()
                    {
                        title = "Негры",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=44"
                    },
                    new MenuItem()
                    {
                        title = "Ноги",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=412"
                    },
                    new MenuItem()
                    {
                        title = "Папики",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=47"
                    },
                    new MenuItem()
                    {
                        title = "Парни (соло)",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=54"
                    },
                    new MenuItem()
                    {
                        title = "Пухленькие",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=392"
                    },
                    new MenuItem()
                    {
                        title = "Ретро",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=77"
                    },
                    new MenuItem()
                    {
                        title = "Татуированные Мужчины",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=552"
                    },
                    new MenuItem()
                    {
                        title = "Фетиш",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=52"
                    }
                };

                menu.Add(new MenuItem()
                {
                    title = $"Категория: {submenu.FirstOrDefault(i => i.playlist_url!.EndsWith($"&c={c}")).title ?? "все"}",
                    playlist_url = "submenu",
                    submenu = submenu
                });
            }
            else if (plugin == "phub" || plugin == "phubprem")
            {
                var submenu = new List<MenuItem>()
                {
                    new MenuItem()
                    {
                        title = "Все",
                        playlist_url = url + $"?hd={hd}&sort={sort}"
                    },
                    new MenuItem()
                    {
                        title = "Женский Выбор",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=73"
                    },
                    new MenuItem()
                    {
                        title = "Русское",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=99"
                    },
                    new MenuItem()
                    {
                        title = "Немецкое",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=95"
                    },
                    new MenuItem()
                    {
                        title = "60FPS",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=105"
                    },
                    new MenuItem()
                    {
                        title = "Азиатки",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=1"
                    },
                    new MenuItem()
                    {
                        title = "Анальный секс",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=35"
                    },
                    new MenuItem()
                    {
                        title = "Арабское",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=98"
                    },
                    new MenuItem()
                    {
                        title = "БДСМ",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=10"
                    },
                    new MenuItem()
                    {
                        title = "Безобидный контент",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=221"
                    },
                    new MenuItem()
                    {
                        title = "Бисексуалы",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=76"
                    },
                    new MenuItem()
                    {
                        title = "Блондинки",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=9"
                    },
                    new MenuItem()
                    {
                        title = "Большая грудь",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=8"
                    },
                    new MenuItem()
                    {
                        title = "Большие члены",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=7"
                    },
                    new MenuItem()
                    {
                        title = "Бразильское",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=102"
                    },
                    new MenuItem()
                    {
                        title = "Британское",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=96"
                    },
                    new MenuItem()
                    {
                        title = "Брызги",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=69"
                    },
                    new MenuItem()
                    {
                        title = "Брюнетки",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=11"
                    },
                    new MenuItem()
                    {
                        title = "Буккаке",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=14"
                    },
                    new MenuItem()
                    {
                        title = "В школе",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=88"
                    },
                    new MenuItem()
                    {
                        title = "Веб-камера",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=61"
                    },
                    new MenuItem()
                    {
                        title = "Вечеринки",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=53"
                    },
                    new MenuItem()
                    {
                        title = "Гонзо",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=41"
                    },
                    new MenuItem()
                    {
                        title = "Грубый секс",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=67"
                    },
                    new MenuItem()
                    {
                        title = "Групповуха",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=80"
                    },
                    new MenuItem()
                    {
                        title = "Двойное проникновение",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=72"
                    },
                    new MenuItem()
                    {
                        title = "Девушки (соло)",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=492"
                    },
                    new MenuItem()
                    {
                        title = "Дрочит",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=20"
                    },
                    new MenuItem()
                    {
                        title = "Европейцы",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=55"
                    },
                    new MenuItem()
                    {
                        title = "Женский оргазм",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=502"
                    },
                    new MenuItem()
                    {
                        title = "Жесткий секс",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=21"
                    },
                    new MenuItem()
                    {
                        title = "За кадром",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=141"
                    },
                    new MenuItem()
                    {
                        title = "Звезды",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=12"
                    },
                    new MenuItem()
                    {
                        title = "Золотой дождь",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=211"
                    },
                    new MenuItem()
                    {
                        title = "Зрелые",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=28"
                    },
                    new MenuItem()
                    {
                        title = "Игрушки",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=23"
                    },
                    new MenuItem()
                    {
                        title = "Индийское",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=101"
                    },
                    new MenuItem()
                    {
                        title = "Итальянское",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=97"
                    },
                    new MenuItem()
                    {
                        title = "Кастинги",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=90"
                    },
                    new MenuItem()
                    {
                        title = "Колледж",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=79"
                    },
                    new MenuItem()
                    {
                        title = "Кончают",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=16"
                    },
                    new MenuItem()
                    {
                        title = "Корейское",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=103"
                    },
                    new MenuItem()
                    {
                        title = "Косплей",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=241"
                    },
                    new MenuItem()
                    {
                        title = "Красотки",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=5"
                    },
                    new MenuItem()
                    {
                        title = "Кремпай",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=15"
                    },
                    new MenuItem()
                    {
                        title = "Кунилингус",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=131"
                    },
                    new MenuItem()
                    {
                        title = "Курящие",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=91"
                    },
                    new MenuItem()
                    {
                        title = "Латинки",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=26"
                    },
                    new MenuItem()
                    {
                        title = "Любительское",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=3"
                    },
                    new MenuItem()
                    {
                        title = "Маленькая грудь",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=59"
                    },
                    new MenuItem()
                    {
                        title = "Мамочки",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=29"
                    },
                    new MenuItem()
                    {
                        title = "Массаж",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=78"
                    },
                    new MenuItem()
                    {
                        title = "Мастурбация",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=22"
                    },
                    new MenuItem()
                    {
                        title = "Межрассовый Секс",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=25"
                    },
                    new MenuItem()
                    {
                        title = "Минет",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=13"
                    },
                    new MenuItem()
                    {
                        title = "Мулаты",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=17"
                    },
                    new MenuItem()
                    {
                        title = "Мультики",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=86"
                    },
                    new MenuItem()
                    {
                        title = "Мускулистые Мужчины",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=512"
                    },
                    new MenuItem()
                    {
                        title = "На публике",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=24"
                    },
                    new MenuItem()
                    {
                        title = "Ноги",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=93"
                    },
                    new MenuItem()
                    {
                        title = "Няни",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=89"
                    },
                    new MenuItem()
                    {
                        title = "Пародия",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=201"
                    },
                    new MenuItem()
                    {
                        title = "Пенсионеры / подростки",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=181"
                    },
                    new MenuItem()
                    {
                        title = "Подростки",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=37"
                    },
                    new MenuItem()
                    {
                        title = "Попки",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=4"
                    },
                    new MenuItem()
                    {
                        title = "Приколы",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=32"
                    },
                    new MenuItem()
                    {
                        title = "Ретро",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=43"
                    },
                    new MenuItem()
                    {
                        title = "Рогоносцы",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=242"
                    },
                    new MenuItem()
                    {
                        title = "Ролевые Игры",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=81"
                    },
                    new MenuItem()
                    {
                        title = "Романтическое",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=522"
                    },
                    new MenuItem()
                    {
                        title = "Рыжие",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=42"
                    },
                    new MenuItem()
                    {
                        title = "Секс втроем",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=65"
                    },
                    new MenuItem()
                    {
                        title = "Секс-оргия",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=2"
                    },
                    new MenuItem()
                    {
                        title = "Семейные фантазии",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=444"
                    },
                    new MenuItem()
                    {
                        title = "Страпон",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=542"
                    },
                    new MenuItem()
                    {
                        title = "Стриптиз",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=33"
                    },
                    new MenuItem()
                    {
                        title = "Татуированные Женщины",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=562"
                    },
                    new MenuItem()
                    {
                        title = "Толстушки",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=6"
                    },
                    new MenuItem()
                    {
                        title = "Трансвеститы",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=83"
                    },
                    new MenuItem()
                    {
                        title = "Удовлетворение пальцами",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=592"
                    },
                    new MenuItem()
                    {
                        title = "Фетиш",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=18"
                    },
                    new MenuItem()
                    {
                        title = "Фистинг",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=19"
                    },
                    new MenuItem()
                    {
                        title = "Французское",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=94"
                    },
                    new MenuItem()
                    {
                        title = "Хентай",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=36"
                    },
                    new MenuItem()
                    {
                        title = "Чешское",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=100"
                    },
                    new MenuItem()
                    {
                        title = "Японцы",
                        playlist_url = url + $"?hd={hd}&sort={sort}&c=111"
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

        async public static ValueTask<StreamItem> StreamLinks(string video_uri, string list_uri, string host, string vkey, Func<string, ValueTask<string>> onresult)
        {
            if (string.IsNullOrEmpty(vkey))
                return null;

            string html = await onresult.Invoke($"{host}/view_video.php?viewkey={vkey}");
            if (html == null)
                return null;

            string hls = Regex.Match(html, "\"hls\",\"videoUrl\":\"([^\"]+urlset\\\\/master\\.m3u[^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrEmpty(hls))
            {
                var qualitys = new Dictionary<string, string>();

                foreach (string q in new string[] { "1080", "720", "480", "240" })
                {
                    string video = Regex.Match(html, $"\"videoUrl\":\"([^\"]+)\",\"quality\":\"{q}\"").Groups[1].Value;
                    if (!string.IsNullOrEmpty(video))
                        qualitys.TryAdd($"{q}p", video.Replace("\\", "").Replace("///", "//"));
                }

                if (qualitys.Count > 0)
                {
                    return new StreamItem()
                    {
                        qualitys = qualitys,
                        recomends = Playlist(video_uri, list_uri, html, related: true)
                    };
                }
            }

            if (string.IsNullOrEmpty(hls))
            {
                hls = getDirectLinks(html);
                if (string.IsNullOrEmpty(hls))
                    return null;
            }

            return new StreamItem()
            {
                qualitys = new Dictionary<string, string>()
                {
                    ["auto"] = hls.Replace("\\", "").Replace("///", "//")
                },
                recomends = Playlist(video_uri, list_uri, html, related: true)
            };
        }


        public static int Pages(in string html)
        { 
            if (string.IsNullOrEmpty(html))
                return 0;

            if (!html.Contains("class=\"page_number\""))
                return 1;

            int maxpage = 0;
            foreach (Match match in new Regex("class=\"page_number\"><a [^>]+>([0-9]+)<").Matches(html))
            {
                if (int.TryParse(match.Groups[1].Value, out int page) && page > maxpage)
                    maxpage = page;
            }

            // модель 6, навигация 5
            if (4 >= maxpage)
                return maxpage;

            return 0;
        }


        #region getDirectLinks
        static string getDirectLinks(in string pageCode)
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
                    string param = vars.Find(x => x.name == curr).param;
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

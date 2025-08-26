using HtmlAgilityPack;
using Shared.Models.SISI.Base;
using Shared.Models.SISI.OnResult;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class XhamsterTo
    {
        public static ValueTask<string> InvokeHtml(string host, string plugin, string search, string c, string q, string sort, int pg, Func<string, ValueTask<string>> onresult)
        {
            string url;

            if (!string.IsNullOrWhiteSpace(search))
            {
                url = $"{host}/search/{HttpUtility.UrlEncode(search)}?page={pg}";
            }
            else
            {
                switch (plugin ?? "")
                {
                    case "xmrsml":
                        url = $"{host}/shemale";
                        break;
                    case "xmrgay":
                        url = $"{host}/gay";
                        break;
                    default:
                        url = host;
                        break;
                }

                if (!string.IsNullOrEmpty(c))
                    url += $"/categories/{c}";

                if (!string.IsNullOrEmpty(q))
                    url += $"/{q}";

                switch (sort ?? "")
                {
                    case "newest":
                        url += "/newest";
                        break;
                    case "best":
                        url += "/best";
                        break;
                    default:
                        break;
                }

                if (pg > 0)
                    url += $"/{pg}";
            }

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, in string html, Func<PlaylistItem, PlaylistItem> onplaylist = null)
        {
            if (string.IsNullOrEmpty(html))
                return new List<PlaylistItem>();

            string section = html;

            if (html.Contains("mixed-section"))
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                string single = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'mixed-section')]")?.InnerHtml;
                if (single != null)
                    section = single;
            }

            var rows = Regex.Split(section, "(<div class=\"thumb-list__item video-thumb|thumb-list-mobile-item)");
            var playlists = new List<PlaylistItem>(rows.Length);

            foreach (string row in rows.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row) || row.Contains("badge_premium"))
                    continue;

                var g = Regex.Match(row, "__nam[^\"]+\" href=\"https?://[^/]+/([^\"]+)\"([^>]+)?>(<!--[^-]+-->)?([^<]+)").Groups;
                string title = g[4].Value;
                string href = g[1].Value;

                if (!string.IsNullOrEmpty(href) && !string.IsNullOrWhiteSpace(title))
                {
                    string duration = Regex.Match(row, "data-role=\"video-duration\"><[^>]+>([^<]+)").Groups[1].Value;
                    if (string.IsNullOrEmpty(duration))
                        duration = Regex.Match(row, "datetime=\"([^\"]+)\"").Groups[1].Value;

                    string img = Regex.Match(row, " srcset=\"([^\"]+)\"").Groups[1].Value;
                    if (!img.StartsWith("http") || img.Contains("(w:16,h:9)"))
                    {
                        img = Regex.Match(row, "thumb-image-container__image\" src=\"([^\"]+)\"").Groups[1].Value;
                        if (!img.StartsWith("http"))
                            img = Regex.Match(row, "<noscript><img src=\"([^\"]+)\"").Groups[1].Value.Trim();
                    }

                    if (!img.StartsWith("http"))
                        continue;

                    var pl = new PlaylistItem()
                    {
                        name = title,
                        video = $"{uri}?uri={href}",
                        picture = img,
                        quality = row.Contains("-hd") ? "HD" : row.Contains("-uhd") ? "4K" : null,
                        preview = Regex.Match(row, "data-previewvideo=\"([^\"]+)\"").Groups[1].Value,
                        time = duration?.Trim(),
                        json = true,
                        related = true,
                        bookmark = new Bookmark()
                        {
                            site = "xmr",
                            href = href,
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

        public static List<MenuItem> Menu(string host, string plugin, string c, string q, string sort)
        {
            host = string.IsNullOrWhiteSpace(host) ? string.Empty : $"{host}/";

            var menu = new List<MenuItem>()
            {
                new MenuItem()
                {
                    title = "Поиск",
                    search_on = "search_on",
                    playlist_url = host + plugin,
                },
                new MenuItem()
                {
                    title = $"Качество: {(q == "4k" ? "2160p" : "Любое")}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem("Любое", host + $"{plugin}?c={c}&sort={sort}"),
                        new MenuItem("2160p", host + $"{plugin}?c={c}&sort={sort}&q=4k")
                    }
                },
                new MenuItem()
                {
                    title = $"Сортировка: {(sort == "newest" ? "Новинки" : sort == "best" ? "Лучшие" :"В тренде")}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem("В тренде", host + $"{plugin}?c={c}&q={q}&sort=trend"),
                        new MenuItem("Самые новые", host + $"{plugin}?c={c}&q={q}&sort=newest"),
                        new MenuItem("Лучшие видео", host + $"{plugin}?c={c}&q={q}&sort=best")
                    }
                },
                new MenuItem()
                {
                    title = $"Ориентация: {(plugin == "xmrgay" ? "Геи" : plugin == "xmrsml" ? "Трансы" :"Гетеро")}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem("Гетеро", host + "xmr"),
                        new MenuItem("Геи", host + "xmrgay"),
                        new MenuItem("Трансы", host + "xmrsml")
                    }
                }
            };

            if (plugin == "xmr")
            {
                var submenu = new List<MenuItem>()
                {
                    new MenuItem("Все", host + $"{plugin}?sort={sort}&q={q}"),
                    new MenuItem("Русское", host + $"{plugin}?sort={sort}&q={q}&c=russian"),
                    new MenuItem("Секс втроем", host + $"{plugin}?sort={sort}&q={q}&c=threesome"),
                    new MenuItem("Азиатское", host + $"{plugin}?sort={sort}&q={q}&c=asian"),
                    new MenuItem("Анал", host + $"{plugin}?sort={sort}&q={q}&c=anal"),
                    new MenuItem("Арабское", host + $"{plugin}?sort={sort}&q={q}&c=arab"),
                    new MenuItem("АСМР", host + $"{plugin}?sort={sort}&q={q}&c=asmr"),
                    new MenuItem("Бабки", host + $"{plugin}?sort={sort}&q={q}&c=granny"),
                    new MenuItem("БДСМ", host + $"{plugin}?sort={sort}&q={q}&c=bdsm"),
                    new MenuItem("Би", host + $"{plugin}?sort={sort}&q={q}&c=bisexual"),
                    new MenuItem("Большие жопы", host + $"{plugin}?sort={sort}&q={q}&c=big-ass"),
                    new MenuItem("Большие задницы", host + $"{plugin}?sort={sort}&q={q}&c=pawg"),
                    new MenuItem("Большие сиськи", host + $"{plugin}?sort={sort}&q={q}&c=big-tits"),
                    new MenuItem("Большой член", host + $"{plugin}?sort={sort}&q={q}&c=big-cock"),
                    new MenuItem("Британское", host + $"{plugin}?sort={sort}&q={q}&c=british"),
                    new MenuItem("В возрасте", host + $"{plugin}?sort={sort}&q={q}&c=mature"),
                    new MenuItem("Вебкамера", host + $"{plugin}?sort={sort}&q={q}&c=webcam"),
                    new MenuItem("Винтаж", host + $"{plugin}?sort={sort}&q={q}&c=vintage"),
                    new MenuItem("Волосатые", host + $"{plugin}?sort={sort}&q={q}&c=hairy"),
                    new MenuItem("Голые мужчины одетые женщины", host + $"{plugin}?sort={sort}&q={q}&c=cfnm"),
                    new MenuItem("Групповой секс", host + $"{plugin}?sort={sort}&q={q}&c=group-sex"),
                    new MenuItem("Гэнгбэнг", host + $"{plugin}?sort={sort}&q={q}&c=gangbang"),
                    new MenuItem("Дилдо", host + $"{plugin}?sort={sort}&q={q}&c=dildo"),
                    new MenuItem("Домашнее порно", host + $"{plugin}?sort={sort}&q={q}&c=homemade"),
                    new MenuItem("Дрочка ступнями", host + $"{plugin}?sort={sort}&q={q}&c=footjob"),
                    new MenuItem("Женское доминирование", host + $"{plugin}?sort={sort}&q={q}&c=femdom"),
                    new MenuItem("Жиробасина", host + $"{plugin}?sort={sort}&q={q}&c=ssbbw"),
                    new MenuItem("Жопа", host + $"{plugin}?sort={sort}&q={q}&c=ass"),
                    new MenuItem("Застряла", host + $"{plugin}?sort={sort}&q={q}&c=stuck"),
                    new MenuItem("Знаменитость", host + $"{plugin}?sort={sort}&q={q}&c=celebrity"),
                    new MenuItem("Игра", host + $"{plugin}?sort={sort}&q={q}&c=game"),
                    new MenuItem("История", host + $"{plugin}?sort={sort}&q={q}&c=story"),
                    new MenuItem("Кастинг", host + $"{plugin}?sort={sort}&q={q}&c=casting"),
                    new MenuItem("Комический", host + $"{plugin}?sort={sort}&q={q}&c=comic"),
                    new MenuItem("Кончина", host + $"{plugin}?sort={sort}&q={q}&c=cumshot"),
                    new MenuItem("Кремовый пирог", host + $"{plugin}?sort={sort}&q={q}&c=creampie"),
                    new MenuItem("Латина", host + $"{plugin}?sort={sort}&q={q}&c=latina"),
                    new MenuItem("Лесбиянка", host + $"{plugin}?sort={sort}&q={q}&c=lesbian"),
                    new MenuItem("Лизать киску", host + $"{plugin}?sort={sort}&q={q}&c=eating-pussy"),
                    new MenuItem("Любительское порно", host + $"{plugin}?sort={sort}&q={q}&c=amateur"),
                    new MenuItem("Массаж", host + $"{plugin}?sort={sort}&q={q}&c=massage"),
                    new MenuItem("Медсестра", host + $"{plugin}?sort={sort}&q={q}&c=nurse"),
                    new MenuItem("Межрасовый секс", host + $"{plugin}?sort={sort}&q={q}&c=interracial"),
                    new MenuItem("МИЛФ", host + $"{plugin}?sort={sort}&q={q}&c=milf"),
                    new MenuItem("Милые", host + $"{plugin}?sort={sort}&q={q}&c=cute"),
                    new MenuItem("Минет", host + $"{plugin}?sort={sort}&q={q}&c=blowjob"),
                    new MenuItem("Миниатюрная", host + $"{plugin}?sort={sort}&q={q}&c=petite"),
                    new MenuItem("Миссионерская поза", host + $"{plugin}?sort={sort}&q={q}&c=missionary"),
                    new MenuItem("Монахиня", host + $"{plugin}?sort={sort}&q={q}&c=nun"),
                    new MenuItem("Мультфильмы", host + $"{plugin}?sort={sort}&q={q}&c=cartoon"),
                    new MenuItem("Негритянки", host + $"{plugin}?sort={sort}&q={q}&c=black"),
                    new MenuItem("Немецкое", host + $"{plugin}?sort={sort}&q={q}&c=german"),
                    new MenuItem("Офис", host + $"{plugin}?sort={sort}&q={q}&c=office"),
                    new MenuItem("Первый раз", host + $"{plugin}?sort={sort}&q={q}&c=first-time"),
                    new MenuItem("Пляж", host + $"{plugin}?sort={sort}&q={q}&c=beach"),
                    new MenuItem("Порно для женщин", host + $"{plugin}?sort={sort}&q={q}&c=porn-for-women"),
                    new MenuItem("Реслинг", host + $"{plugin}?sort={sort}&q={q}&c=wrestling"),
                    new MenuItem("Рогоносцы", host + $"{plugin}?sort={sort}&q={q}&c=cuckold"),
                    new MenuItem("Романтический", host + $"{plugin}?sort={sort}&q={q}&c=romantic"),
                    new MenuItem("Свингеры", host + $"{plugin}?sort={sort}&q={q}&c=swingers"),
                    new MenuItem("Сквирт", host + $"{plugin}?sort={sort}&q={q}&c=squirting"),
                    new MenuItem("Старик", host + $"{plugin}?sort={sort}&q={q}&c=old-man"),
                    new MenuItem("Старые с молодыми", host + $"{plugin}?sort={sort}&q={q}&c=old-young"),
                    new MenuItem("Тинейджеры (18+)", host + $"{plugin}?sort={sort}&q={q}&c=teen"),
                    new MenuItem("Толстушки", host + $"{plugin}?sort={sort}&q={q}&c=bbw"),
                    new MenuItem("Тренажерный зал", host + $"{plugin}?sort={sort}&q={q}&c=gym"),
                    new MenuItem("Узкая киска", host + $"{plugin}?sort={sort}&q={q}&c=tight-pussy"),
                    new MenuItem("Французское", host + $"{plugin}?sort={sort}&q={q}&c=french"),
                    new MenuItem("Футанари", host + $"{plugin}?sort={sort}&q={q}&c=futanari"),
                    new MenuItem("Хардкор", host + $"{plugin}?sort={sort}&q={q}&c=hardcore"),
                    new MenuItem("Хенджоб", host + $"{plugin}?sort={sort}&q={q}&c=handjob"),
                    new MenuItem("Хентай", host + $"{plugin}?sort={sort}&q={q}&c=hentai"),
                    new MenuItem("Японское", host + $"{plugin}?sort={sort}&q={q}&c=japanese")
                };

                menu.Add(new MenuItem()
                {
                    title = $"Категория: {submenu.FirstOrDefault(i => i.playlist_url!.EndsWith($"&c={c}")).title ?? "все"}",
                    playlist_url = "submenu",
                    submenu = submenu
                });
            }
            else if (plugin == "xmrgay")
            {
                var submenu = new List<MenuItem>()
                {
                    new MenuItem("Все", host + $"{plugin}?sort={sort}&q={q}"),
                    new MenuItem("Russian", host + $"{plugin}?sort={sort}&q={q}&c=russian"),
                    new MenuItem("Threesome", host + $"{plugin}?sort={sort}&q={q}&c=threesome"),
                    new MenuItem("Азиатское", host + $"{plugin}?sort={sort}&q={q}&c=asian"),
                    new MenuItem("БДСМ", host + $"{plugin}?sort={sort}&q={q}&c=bdsm"),
                    new MenuItem("Без презерватива", host + $"{plugin}?sort={sort}&q={q}&c=bareback"),
                    new MenuItem("Большие дырки", host + $"{plugin}?sort={sort}&q={q}&c=gaping"),
                    new MenuItem("Большой член", host + $"{plugin}?sort={sort}&q={q}&c=big-cock"),
                    new MenuItem("Буккаке", host + $"{plugin}?sort={sort}&q={q}&c=bukkake"),
                    new MenuItem("Вебкамера", host + $"{plugin}?sort={sort}&q={q}&c=webcam"),
                    new MenuItem("Винтаж", host + $"{plugin}?sort={sort}&q={q}&c=vintage"),
                    new MenuItem("Глорихол", host + $"{plugin}?sort={sort}&q={q}&c=glory-hole"),
                    new MenuItem("Групповой секс", host + $"{plugin}?sort={sort}&q={q}&c=group-sex"),
                    new MenuItem("Гэнгбэнг", host + $"{plugin}?sort={sort}&q={q}&c=gangbang"),
                    new MenuItem("Дедушка", host + $"{plugin}?sort={sort}&q={q}&c=grandpa"),
                    new MenuItem("Дилдо", host + $"{plugin}?sort={sort}&q={q}&c=dildo"),
                    new MenuItem("Кончать на фотографии", host + $"{plugin}?sort={sort}&q={q}&c=cum-tribute"),
                    new MenuItem("Кончина", host + $"{plugin}?sort={sort}&q={q}&c=cumshot"),
                    new MenuItem("Красавчик", host + $"{plugin}?sort={sort}&q={q}&c=hunk"),
                    new MenuItem("Кремовый пирог", host + $"{plugin}?sort={sort}&q={q}&c=creampie"),
                    new MenuItem("Маленький член", host + $"{plugin}?sort={sort}&q={q}&c=small-cock"),
                    new MenuItem("Массаж", host + $"{plugin}?sort={sort}&q={q}&c=massage"),
                    new MenuItem("Мастурбация", host + $"{plugin}?sort={sort}&q={q}&c=masturbation"),
                    new MenuItem("Медведь", host + $"{plugin}?sort={sort}&q={q}&c=bear"),
                    new MenuItem("Межрасовый секс", host + $"{plugin}?sort={sort}&q={q}&c=interracial"),
                    new MenuItem("Минет", host + $"{plugin}?sort={sort}&q={q}&c=blowjob"),
                    new MenuItem("Молодые", host + $"{plugin}?sort={sort}&q={q}&c=young"),
                    new MenuItem("На природе", host + $"{plugin}?sort={sort}&q={q}&c=outdoor"),
                    new MenuItem("Негры", host + $"{plugin}?sort={sort}&q={q}&c=black"),
                    new MenuItem("Папочка", host + $"{plugin}?sort={sort}&q={q}&c=daddy"),
                    new MenuItem("Пляж", host + $"{plugin}?sort={sort}&q={q}&c=beach"),
                    new MenuItem("Пухляш", host + $"{plugin}?sort={sort}&q={q}&c=chubby"),
                    new MenuItem("Раздевалка", host + $"{plugin}?sort={sort}&q={q}&c=locker-room"),
                    new MenuItem("Рестлинг", host + $"{plugin}?sort={sort}&q={q}&c=wrestling"),
                    new MenuItem("Секс игрушки", host + $"{plugin}?sort={sort}&q={q}&c=sex-toy"),
                    new MenuItem("Сладкий мальчик", host + $"{plugin}?sort={sort}&q={q}&c=twink"),
                    new MenuItem("Соло", host + $"{plugin}?sort={sort}&q={q}&c=solo"),
                    new MenuItem("Спанкинг", host + $"{plugin}?sort={sort}&q={q}&c=spanking"),
                    new MenuItem("Старые с молодыми", host + $"{plugin}?sort={sort}&q={q}&c=old-young"),
                    new MenuItem("Стриптиз", host + $"{plugin}?sort={sort}&q={q}&c=striptease"),
                    new MenuItem("Толстые", host + $"{plugin}?sort={sort}&q={q}&c=fat"),
                    new MenuItem("Трансвеститы", host + $"{plugin}?sort={sort}&q={q}&c=crossdresser"),
                    new MenuItem("Фистинг", host + $"{plugin}?sort={sort}&q={q}&c=fisting"),
                    new MenuItem("Хенджоб", host + $"{plugin}?sort={sort}&q={q}&c=handjob"),
                    new MenuItem("Хентай", host + $"{plugin}?sort={sort}&q={q}&c=hentai"),
                    new MenuItem("Эмобой", host + $"{plugin}?sort={sort}&q={q}&c=emo")
                };

                menu.Add(new MenuItem()
                {
                    title = $"Категория: {submenu.FirstOrDefault(i => i.playlist_url!.EndsWith($"&c={c}")).title ?? "все"}",
                    playlist_url = "submenu",
                    submenu = submenu
                });
            }
            else if (plugin == "xmrsml")
            {
                var submenu = new List<MenuItem>()
                {
                    new MenuItem("Все", host + $"{plugin}?sort={sort}&q={q}"),
                    new MenuItem("Russian", host + $"{plugin}?sort={sort}&q={q}&c=russian"),
                    new MenuItem("Cuckold", host + $"{plugin}?sort={sort}&q={q}&c=cuckold"),
                    new MenuItem("Азиатское", host + $"{plugin}?sort={sort}&q={q}&c=asian"),
                    new MenuItem("БДСМ", host + $"{plugin}?sort={sort}&q={q}&c=bdsm"),
                    new MenuItem("Без презерватива", host + $"{plugin}?sort={sort}&q={q}&c=bareback"),
                    new MenuItem("Блондинки", host + $"{plugin}?sort={sort}&q={q}&c=blonde"),
                    new MenuItem("Большие жопы", host + $"{plugin}?sort={sort}&q={q}&c=big-ass"),
                    new MenuItem("Большой член", host + $"{plugin}?sort={sort}&q={q}&c=big-cock"),
                    new MenuItem("Вебкамера", host + $"{plugin}?sort={sort}&q={q}&c=webcam"),
                    new MenuItem("Винтаж", host + $"{plugin}?sort={sort}&q={q}&c=vintage"),
                    new MenuItem("Групповой секс", host + $"{plugin}?sort={sort}&q={q}&c=group-sex"),
                    new MenuItem("Гэнгбэнг", host + $"{plugin}?sort={sort}&q={q}&c=gangbang"),
                    new MenuItem("Домашнее", host + $"{plugin}?sort={sort}&q={q}&c=homemade"),
                    new MenuItem("Золотой дождь", host + $"{plugin}?sort={sort}&q={q}&c=pissing"),
                    new MenuItem("Кремовый пирог", host + $"{plugin}?sort={sort}&q={q}&c=creampie"),
                    new MenuItem("Латекс", host + $"{plugin}?sort={sort}&q={q}&c=latex"),
                    new MenuItem("Латина", host + $"{plugin}?sort={sort}&q={q}&c=latina"),
                    new MenuItem("Ледибой", host + $"{plugin}?sort={sort}&q={q}&c=ladyboy"),
                    new MenuItem("Ловушка", host + $"{plugin}?sort={sort}&q={q}&c=trap"),
                    new MenuItem("Любительское порно", host + $"{plugin}?sort={sort}&q={q}&c=amateur"),
                    new MenuItem("Маленькие сиськи", host + $"{plugin}?sort={sort}&q={q}&c=small-tits"),
                    new MenuItem("Мастурбация", host + $"{plugin}?sort={sort}&q={q}&c=masturbation"),
                    new MenuItem("Межрасовый секс", host + $"{plugin}?sort={sort}&q={q}&c=interracial"),
                    new MenuItem("Минет", host + $"{plugin}?sort={sort}&q={q}&c=blowjob"),
                    new MenuItem("Миниатюрная", host + $"{plugin}?sort={sort}&q={q}&c=petite"),
                    new MenuItem("На природе", host + $"{plugin}?sort={sort}&q={q}&c=outdoor"),
                    new MenuItem("Нижнее белье", host + $"{plugin}?sort={sort}&q={q}&c=lingerie"),
                    new MenuItem("От первого лица", host + $"{plugin}?sort={sort}&q={q}&c=pov"),
                    new MenuItem("Парень трахает транса", host + $"{plugin}?sort={sort}&q={q}&c=guy-fucks-shemale"),
                    new MenuItem("Подростки", host + $"{plugin}?sort={sort}&q={q}&c=teen"),
                    new MenuItem("Рыжие", host + $"{plugin}?sort={sort}&q={q}&c=redhead"),
                    new MenuItem("Секс втроем", host + $"{plugin}?sort={sort}&q={q}&c=threesome"),
                    new MenuItem("Секс игрушки", host + $"{plugin}?sort={sort}&q={q}&c=sex-toy"),
                    new MenuItem("Соло", host + $"{plugin}?sort={sort}&q={q}&c=solo"),
                    new MenuItem("Татуировки", host + $"{plugin}?sort={sort}&q={q}&c=tattoo"),
                    new MenuItem("Толстушки", host + $"{plugin}?sort={sort}&q={q}&c=bbw"),
                    new MenuItem("Транс трахает девушку", host + $"{plugin}?sort={sort}&q={q}&c=shemale-fucks-girl"),
                    new MenuItem("Транс трахает парня", host + $"{plugin}?sort={sort}&q={q}&c=shemale-fucks-guy"),
                    new MenuItem("Транс трахает транса", host + $"{plugin}?sort={sort}&q={q}&c=shemale-fucks-shemale"),
                    new MenuItem("Трансвестит", host + $"{plugin}?sort={sort}&q={q}&c=transgender"),
                    new MenuItem("Фетиш", host + $"{plugin}?sort={sort}&q={q}&c=fetish"),
                    new MenuItem("Хардкор", host + $"{plugin}?sort={sort}&q={q}&c=hardcore"),
                    new MenuItem("Хенджоб", host + $"{plugin}?sort={sort}&q={q}&c=handjob"),
                    new MenuItem("Хентай", host + $"{plugin}?sort={sort}&q={q}&c=hentai"),
                    new MenuItem("Хорошенькая", host + $"{plugin}?sort={sort}&q={q}&c=pretty"),
                    new MenuItem("Чернокожие", host + $"{plugin}?sort={sort}&q={q}&c=black"),
                    new MenuItem("Чулки", host + $"{plugin}?sort={sort}&q={q}&c=stockings"),
                    new MenuItem("Японское порно", host + $"{plugin}?sort={sort}&q={q}&c=japanese")
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

        async public static ValueTask<StreamItem> StreamLinks(string uri, string host, string url, Func<string, ValueTask<string>> onresult)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            string html = await onresult.Invoke($"{host}/{url}");
            if (html == null)
                return null;

            string stream_link = Regex.Match(html, "\"h264\":\\[\\{\"url\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
            if (!stream_link.Contains(".m3u"))
            {
                stream_link = Regex.Match(html, "rel=\"preload\" href=\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                if (!stream_link.Contains(".m3u"))
                    return null;
            }

            if (stream_link.StartsWith("/"))
                stream_link = host + stream_link;

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

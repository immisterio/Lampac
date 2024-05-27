using Lampac.Models.SISI;
using Shared.Model;
using Shared.Model.SISI;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class XhamsterTo
    {
        public static ValueTask<string?> InvokeHtml(string host, string plugin, string? search, string? c, string? q, string? sort, int pg, Func<string, ValueTask<string?>> onresult)
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
                        url = "/best/weekly";
                        break;
                    default:
                        break;
                }

                if (pg > 0)
                    url += $"/{pg}";
            }

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, string? html, Func<PlaylistItem, PlaylistItem>? onplaylist = null)
        {
            var playlists = new List<PlaylistItem>() { Capacity = 50 };

            if (string.IsNullOrEmpty(html))
                return playlists;

            string section = html.Contains("mixed-section") ? html.Split("mixed-section")[1] : html;

            foreach (string row in Regex.Split(section, "(<div class=\"thumb-list__item video-thumb|thumb-list-mobile-item)").Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row) || row.Contains("badge_premium"))
                    continue;

                var g = Regex.Match(row, "__nam[^\"]+\" href=\"https?://[^/]+/([^\"]+)\"([^>]+)?>(<!--[^-]+-->)?([^<]+)").Groups;
                string title = g[4].Value;
                string href = g[1].Value;

                if (!string.IsNullOrEmpty(href) && !string.IsNullOrWhiteSpace(title))
                {
                    string duration = Regex.Match(row, "<div class=\"thumb-image-container__duration\">([^<]+)</div>").Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(duration))
                    {
                        duration = Regex.Match(row, "<span (data-role-video-duration|data-role=\"video-duration\")>([^<]+)</span>").Groups[2].Value;
                        if (string.IsNullOrWhiteSpace(duration))
                            duration = Regex.Match(row, "datetime=\"([^\"]+)\"").Groups[1].Value;
                    }

                    string img = Regex.Match(row, "thumb-image-container__image\" src=\"([^\"]+)\"").Groups[1].Value;
                    if (!img.StartsWith("http"))
                        img = Regex.Match(row, "<noscript><img src=\"([^\"]+)\"").Groups[1].Value.Trim();

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

        public static List<MenuItem> Menu(string? host, string plugin, string? c, string? q, string? sort)
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
                        new MenuItem()
                        {
                            title = "Любое",
                            playlist_url = host + $"{plugin}?c={c}&sort={sort}"
                        },
                        new MenuItem()
                        {
                            title = "2160p",
                            playlist_url = host + $"{plugin}?c={c}&sort={sort}&q=4k"
                        }
                    }
                },
                new MenuItem()
                {
                    title = $"Сортировка: {(sort == "newest" ? "Новинки" : sort == "best" ? "Лучшие" :"В тренде")}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "В тренде",
                            playlist_url = host + $"{plugin}?c={c}&q={q}&sort=trend"
                        },
                        new MenuItem()
                        {
                            title = "Самые новые",
                            playlist_url = host + $"{plugin}?c={c}&q={q}&sort=newest"
                        },
                        new MenuItem()
                        {
                            title = "Лучшие видео",
                            playlist_url = host + $"{plugin}?c={c}&q={q}&sort=best"
                        }
                    }
                },
                new MenuItem()
                {
                    title = $"Ориентация: {(plugin == "xmrgay" ? "Геи" : plugin == "xmrsml" ? "Трансы" :"Гетеро")}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                    {
                        new MenuItem()
                        {
                            title = "Гетеро",
                            playlist_url = host + "xmr",
                        },
                        new MenuItem()
                        {
                            title = "Геи",
                            playlist_url = host + "xmrgay",
                        },
                        new MenuItem()
                        {
                            title = "Трансы",
                            playlist_url = host + "xmrsml",
                        }
                    }
                }
            };

            if (plugin == "xmr")
            {
                var submenu = new List<MenuItem>()
                {
                    new MenuItem()
                    {
                        title = "Все",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}"
                    },
                    new MenuItem()
                    {
                        title = "Русское",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=russian"
                    },
                    new MenuItem()
                    {
                        title = "Секс втроем",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=threesome"
                    },
                    new MenuItem()
                    {
                        title = "Азиатское",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=asian"
                    },
                    new MenuItem()
                    {
                        title = "Анал",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=anal"
                    },
                    new MenuItem()
                    {
                        title = "Арабское",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=arab"
                    },
                    new MenuItem()
                    {
                        title = "АСМР",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=asmr"
                    },
                    new MenuItem()
                    {
                        title = "Бабки",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=granny"
                    },
                    new MenuItem()
                    {
                        title = "БДСМ",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=bdsm"
                    },
                    new MenuItem()
                    {
                        title = "Би",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=bisexual"
                    },
                    new MenuItem()
                    {
                        title = "Большие жопы",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=big-ass"
                    },
                    new MenuItem()
                    {
                        title = "Большие задницы",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=pawg"
                    },
                    new MenuItem()
                    {
                        title = "Большие сиськи",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=big-tits"
                    },
                    new MenuItem()
                    {
                        title = "Большой член",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=big-cock"
                    },
                    new MenuItem()
                    {
                        title = "Британское",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=british"
                    },
                    new MenuItem()
                    {
                        title = "В возрасте",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=mature"
                    },
                    new MenuItem()
                    {
                        title = "Вебкамера",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=webcam"
                    },
                    new MenuItem()
                    {
                        title = "Винтаж",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=vintage"
                    },
                    new MenuItem()
                    {
                        title = "Волосатые",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=hairy"
                    },
                    new MenuItem()
                    {
                        title = "Голые мужчины одетые женщины",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=cfnm"
                    },
                    new MenuItem()
                    {
                        title = "Групповой секс",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=group-sex"
                    },
                    new MenuItem()
                    {
                        title = "Гэнгбэнг",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=gangbang"
                    },
                    new MenuItem()
                    {
                        title = "Дилдо",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=dildo"
                    },
                    new MenuItem()
                    {
                        title = "Домашнее порно",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=homemade"
                    },
                    new MenuItem()
                    {
                        title = "Дрочка ступнями",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=footjob"
                    },
                    new MenuItem()
                    {
                        title = "Женское доминирование",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=femdom"
                    },
                    new MenuItem()
                    {
                        title = "Жиробасина",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=ssbbw"
                    },
                    new MenuItem()
                    {
                        title = "Жопа",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=ass"
                    },
                    new MenuItem()
                    {
                        title = "Застряла",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=stuck"
                    },
                    new MenuItem()
                    {
                        title = "Знаменитость",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=celebrity"
                    },
                    new MenuItem()
                    {
                        title = "Игра",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=game"
                    },
                    new MenuItem()
                    {
                        title = "История",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=story"
                    },
                    new MenuItem()
                    {
                        title = "Кастинг",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=casting"
                    },
                    new MenuItem()
                    {
                        title = "Комический",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=comic"
                    },
                    new MenuItem()
                    {
                        title = "Кончина",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=cumshot"
                    },
                    new MenuItem()
                    {
                        title = "Кремовый пирог",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=creampie"
                    },
                    new MenuItem()
                    {
                        title = "Латина",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=latina"
                    },
                    new MenuItem()
                    {
                        title = "Лесбиянка",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=lesbian"
                    },
                    new MenuItem()
                    {
                        title = "Лизать киску",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=eating-pussy"
                    },
                    new MenuItem()
                    {
                        title = "Любительское порно",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=amateur"
                    },
                    new MenuItem()
                    {
                        title = "Массаж",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=massage"
                    },
                    new MenuItem()
                    {
                        title = "Медсестра",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=nurse"
                    },
                    new MenuItem()
                    {
                        title = "Межрасовый секс",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=interracial"
                    },
                    new MenuItem()
                    {
                        title = "МИЛФ",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=milf"
                    },
                    new MenuItem()
                    {
                        title = "Милые",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=cute"
                    },
                    new MenuItem()
                    {
                        title = "Минет",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=blowjob"
                    },
                    new MenuItem()
                    {
                        title = "Миниатюрная",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=petite"
                    },
                    new MenuItem()
                    {
                        title = "Миссионерская поза",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=missionary"
                    },
                    new MenuItem()
                    {
                        title = "Монахиня",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=nun"
                    },
                    new MenuItem()
                    {
                        title = "Мультфильмы",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=cartoon"
                    },
                    new MenuItem()
                    {
                        title = "Негритянки",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=black"
                    },
                    new MenuItem()
                    {
                        title = "Немецкое",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=german"
                    },
                    new MenuItem()
                    {
                        title = "Офис",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=office"
                    },
                    new MenuItem()
                    {
                        title = "Первый раз",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=first-time"
                    },
                    new MenuItem()
                    {
                        title = "Пляж",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=beach"
                    },
                    new MenuItem()
                    {
                        title = "Порно для женщин",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=porn-for-women"
                    },
                    new MenuItem()
                    {
                        title = "Реслинг",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=wrestling"
                    },
                    new MenuItem()
                    {
                        title = "Рогоносцы",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=cuckold"
                    },
                    new MenuItem()
                    {
                        title = "Романтический",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=romantic"
                    },
                    new MenuItem()
                    {
                        title = "Свингеры",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=swingers"
                    },
                    new MenuItem()
                    {
                        title = "Сквирт",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=squirting"
                    },
                    new MenuItem()
                    {
                        title = "Старик",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=old-man"
                    },
                    new MenuItem()
                    {
                        title = "Старые с молодыми",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=old-young"
                    },
                    new MenuItem()
                    {
                        title = "Тинейджеры (18+)",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=teen"
                    },
                    new MenuItem()
                    {
                        title = "Толстушки",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=bbw"
                    },
                    new MenuItem()
                    {
                        title = "Тренажерный зал",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=gym"
                    },
                    new MenuItem()
                    {
                        title = "Узкая киска",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=tight-pussy"
                    },
                    new MenuItem()
                    {
                        title = "Французское",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=french"
                    },
                    new MenuItem()
                    {
                        title = "Футанари",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=futanari"
                    },
                    new MenuItem()
                    {
                        title = "Хардкор",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=hardcore"
                    },
                    new MenuItem()
                    {
                        title = "Хенджоб",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=handjob"
                    },
                    new MenuItem()
                    {
                        title = "Хентай",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=hentai"
                    },
                    new MenuItem()
                    {
                        title = "Японское",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=japanese"
                    }
                };

                menu.Add(new MenuItem()
                {
                    title = $"Категория: {submenu.FirstOrDefault(i => i.playlist_url!.EndsWith($"&c={c}"))?.title ?? "все"}",
                    playlist_url = "submenu",
                    submenu = submenu
                });
            }
            else if (plugin == "xmrgay")
            {
                var submenu = new List<MenuItem>()
                {
                    new MenuItem()
                    {
                        title = "Все",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}"
                    },
                    new MenuItem()
                    {
                        title = "Russian",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=russian"
                    },
                    new MenuItem()
                    {
                        title = "Threesome",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=threesome"
                    },
                    new MenuItem()
                    {
                        title = "Азиатское",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=asian"
                    },
                    new MenuItem()
                    {
                        title = "БДСМ",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=bdsm"
                    },
                    new MenuItem()
                    {
                        title = "Без презерватива",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=bareback"
                    },
                    new MenuItem()
                    {
                        title = "Большие дырки",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=gaping"
                    },
                    new MenuItem()
                    {
                        title = "Большой член",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=big-cock"
                    },
                    new MenuItem()
                    {
                        title = "Буккаке",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=bukkake"
                    },
                    new MenuItem()
                    {
                        title = "Вебкамера",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=webcam"
                    },
                    new MenuItem()
                    {
                        title = "Винтаж",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=vintage"
                    },
                    new MenuItem()
                    {
                        title = "Глорихол",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=glory-hole"
                    },
                    new MenuItem()
                    {
                        title = "Групповой секс",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=group-sex"
                    },
                    new MenuItem()
                    {
                        title = "Гэнгбэнг",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=gangbang"
                    },
                    new MenuItem()
                    {
                        title = "Дедушка",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=grandpa"
                    },
                    new MenuItem()
                    {
                        title = "Дилдо",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=dildo"
                    },
                    new MenuItem()
                    {
                        title = "Кончать на фотографии",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=cum-tribute"
                    },
                    new MenuItem()
                    {
                        title = "Кончина",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=cumshot"
                    },
                    new MenuItem()
                    {
                        title = "Красавчик",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=hunk"
                    },
                    new MenuItem()
                    {
                        title = "Кремовый пирог",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=creampie"
                    },
                    new MenuItem()
                    {
                        title = "Маленький член",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=small-cock"
                    },
                    new MenuItem()
                    {
                        title = "Массаж",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=massage"
                    },
                    new MenuItem()
                    {
                        title = "Мастурбация",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=masturbation"
                    },
                    new MenuItem()
                    {
                        title = "Медведь",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=bear"
                    },
                    new MenuItem()
                    {
                        title = "Межрасовый секс",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=interracial"
                    },
                    new MenuItem()
                    {
                        title = "Минет",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=blowjob"
                    },
                    new MenuItem()
                    {
                        title = "Молодые",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=young"
                    },
                    new MenuItem()
                    {
                        title = "На природе",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=outdoor"
                    },
                    new MenuItem()
                    {
                        title = "Негры",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=black"
                    },
                    new MenuItem()
                    {
                        title = "Папочка",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=daddy"
                    },
                    new MenuItem()
                    {
                        title = "Пляж",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=beach"
                    },
                    new MenuItem()
                    {
                        title = "Пухляш",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=chubby"
                    },
                    new MenuItem()
                    {
                        title = "Раздевалка",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=locker-room"
                    },
                    new MenuItem()
                    {
                        title = "Рестлинг",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=wrestling"
                    },
                    new MenuItem()
                    {
                        title = "Секс игрушки",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=sex-toy"
                    },
                    new MenuItem()
                    {
                        title = "Сладкий мальчик",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=twink"
                    },
                    new MenuItem()
                    {
                        title = "Соло",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=solo"
                    },
                    new MenuItem()
                    {
                        title = "Спанкинг",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=spanking"
                    },
                    new MenuItem()
                    {
                        title = "Старые с молодыми",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=old-young"
                    },
                    new MenuItem()
                    {
                        title = "Стриптиз",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=striptease"
                    },
                    new MenuItem()
                    {
                        title = "Толстые",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=fat"
                    },
                    new MenuItem()
                    {
                        title = "Трансвеститы",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=crossdresser"
                    },
                    new MenuItem()
                    {
                        title = "Фистинг",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=fisting"
                    },
                    new MenuItem()
                    {
                        title = "Хенджоб",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=handjob"
                    },
                    new MenuItem()
                    {
                        title = "Хентай",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=hentai"
                    },
                    new MenuItem()
                    {
                        title = "Эмобой",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=emo"
                    }
                };

                menu.Add(new MenuItem()
                {
                    title = $"Категория: {submenu.FirstOrDefault(i => i.playlist_url!.EndsWith($"&c={c}"))?.title ?? "все"}",
                    playlist_url = "submenu",
                    submenu = submenu
                });
            }
            else if (plugin == "xmrsml")
            {
                var submenu = new List<MenuItem>()
                {
                    new MenuItem()
                    {
                        title = "Все",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}"
                    },
                    new MenuItem()
                    {
                        title = "Russian",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=russian"
                    },
                    new MenuItem()
                    {
                        title = "Cuckold",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=cuckold"
                    },
                    new MenuItem()
                    {
                        title = "Азиатское",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=asian"
                    },
                    new MenuItem()
                    {
                        title = "БДСМ",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=bdsm"
                    },
                    new MenuItem()
                    {
                        title = "Без презерватива",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=bareback"
                    },
                    new MenuItem()
                    {
                        title = "Блондинки",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=blonde"
                    },
                    new MenuItem()
                    {
                        title = "Большие жопы",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=big-ass"
                    },
                    new MenuItem()
                    {
                        title = "Большой член",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=big-cock"
                    },
                    new MenuItem()
                    {
                        title = "Вебкамера",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=webcam"
                    },
                    new MenuItem()
                    {
                        title = "Винтаж",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=vintage"
                    },
                    new MenuItem()
                    {
                        title = "Групповой секс",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=group-sex"
                    },
                    new MenuItem()
                    {
                        title = "Гэнгбэнг",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=gangbang"
                    },
                    new MenuItem()
                    {
                        title = "Домашнее",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=homemade"
                    },
                    new MenuItem()
                    {
                        title = "Золотой дождь",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=pissing"
                    },
                    new MenuItem()
                    {
                        title = "Кремовый пирог",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=creampie"
                    },
                    new MenuItem()
                    {
                        title = "Латекс",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=latex"
                    },
                    new MenuItem()
                    {
                        title = "Латина",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=latina"
                    },
                    new MenuItem()
                    {
                        title = "Ледибой",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=ladyboy"
                    },
                    new MenuItem()
                    {
                        title = "Ловушка",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=trap"
                    },
                    new MenuItem()
                    {
                        title = "Любительское порно",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=amateur"
                    },
                    new MenuItem()
                    {
                        title = "Маленькие сиськи",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=small-tits"
                    },
                    new MenuItem()
                    {
                        title = "Мастурбация",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=masturbation"
                    },
                    new MenuItem()
                    {
                        title = "Межрасовый секс",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=interracial"
                    },
                    new MenuItem()
                    {
                        title = "Минет",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=blowjob"
                    },
                    new MenuItem()
                    {
                        title = "Миниатюрная",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=petite"
                    },
                    new MenuItem()
                    {
                        title = "На природе",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=outdoor"
                    },
                    new MenuItem()
                    {
                        title = "Нижнее белье",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=lingerie"
                    },
                    new MenuItem()
                    {
                        title = "От первого лица",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=pov"
                    },
                    new MenuItem()
                    {
                        title = "Парень трахает транса",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=guy-fucks-shemale"
                    },
                    new MenuItem()
                    {
                        title = "Подростки",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=teen"
                    },
                    new MenuItem()
                    {
                        title = "Рыжие",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=redhead"
                    },
                    new MenuItem()
                    {
                        title = "Секс втроем",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=threesome"
                    },
                    new MenuItem()
                    {
                        title = "Секс игрушки",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=sex-toy"
                    },
                    new MenuItem()
                    {
                        title = "Соло",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=solo"
                    },
                    new MenuItem()
                    {
                        title = "Татуировки",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=tattoo"
                    },
                    new MenuItem()
                    {
                        title = "Толстушки",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=bbw"
                    },
                    new MenuItem()
                    {
                        title = "Транс трахает девушку",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=shemale-fucks-girl"
                    },
                    new MenuItem()
                    {
                        title = "Транс трахает парня",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=shemale-fucks-guy"
                    },
                    new MenuItem()
                    {
                        title = "Транс трахает транса",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=shemale-fucks-shemale"
                    },
                    new MenuItem()
                    {
                        title = "Трансвестит",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=transgender"
                    },
                    new MenuItem()
                    {
                        title = "Фетиш",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=fetish"
                    },
                    new MenuItem()
                    {
                        title = "Хардкор",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=hardcore"
                    },
                    new MenuItem()
                    {
                        title = "Хенджоб",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=handjob"
                    },
                    new MenuItem()
                    {
                        title = "Хентай",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=hentai"
                    },
                    new MenuItem()
                    {
                        title = "Хорошенькая",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=pretty"
                    },
                    new MenuItem()
                    {
                        title = "Чернокожие",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=black"
                    },
                    new MenuItem()
                    {
                        title = "Чулки",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=stockings"
                    },
                    new MenuItem()
                    {
                        title = "Японское порно",
                        playlist_url = host + $"{plugin}?sort={sort}&q={q}&c=japanese"
                    }
                };

                menu.Add(new MenuItem()
                {
                    title = $"Категория: {submenu.FirstOrDefault(i => i.playlist_url!.EndsWith($"&c={c}"))?.title ?? "все"}",
                    playlist_url = "submenu",
                    submenu = submenu
                });
            }

            return menu;
        }

        async public static ValueTask<StreamItem?> StreamLinks(string uri, string host, string? url, Func<string, ValueTask<string?>> onresult)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            string? html = await onresult.Invoke($"{host}/{url}");
            if (html == null)
                return null;

            string stream_link = Regex.Match(html, "\"h264\":\\[\\{\"url\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
            if (!stream_link.Contains(".m3u"))
                return null;

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

using Shared.Models.SISI.Base;
using Shared.Models.SISI.OnResult;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class EbalovoTo
    {
        public static ValueTask<string> InvokeHtml(string host, string search, string sort, string c, int pg, Func<string, ValueTask<string>> onresult)
        {
            string url = $"{host}/";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url += $"search/{HttpUtility.UrlEncode(search)}/";
            }
            else
            {
                if (!string.IsNullOrEmpty(c))
                {
                    url += $"porno/{c}";

                    if (sort is "porno-online" or "xxx-top")
                        url += $"-rating";

                    url += "/";
                }
                else
                {
                    if (!string.IsNullOrEmpty(sort))
                        url += $"{sort}/";
                }
            }

            if (pg > 1)
                url += $"{pg}/";

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, in string html, Func<PlaylistItem, PlaylistItem> onplaylist = null)
        {
            if (string.IsNullOrEmpty(html))
                return new List<PlaylistItem>();

            var rows = html.Split("<div class=\"item\">");
            var playlists = new List<PlaylistItem>(rows.Length);

            foreach (string row in rows)
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
                        json = true,
                        related = true,
                        bookmark = new Bookmark()
                        {
                            site = "elo",
                            href = link,
                            image = img[2].Value
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
            string url = host + "elo";

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
                            title = "Лучшее",
                            playlist_url = url + $"?c={c}&sort=porno-online"
                        },
                        new MenuItem()
                        {
                            title = "Популярное",
                            playlist_url = url + $"?c={c}&sort=xxx-top"
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
                        title = "CFNM",
                        playlist_url = url + $"?sort={sort}&c=cfnm"
                    },
                    new MenuItem()
                    {
                        title = "pov",
                        playlist_url = url + $"?sort={sort}&c=pov"
                    },
                    new MenuItem()
                    {
                        title = "Анал",
                        playlist_url = url + $"?sort={sort}&c=anal-videos"
                    },
                    new MenuItem()
                    {
                        title = "Анальная дыра",
                        playlist_url = url + $"?sort={sort}&c=gape"
                    },
                    new MenuItem()
                    {
                        title = "Анальная пробка",
                        playlist_url = url + $"?sort={sort}&c=butt-plug-porn"
                    },
                    new MenuItem()
                    {
                        title = "БДСМ",
                        playlist_url = url + $"?sort={sort}&c=bdsm-porn"
                    },
                    new MenuItem()
                    {
                        title = "Блондинки",
                        playlist_url = url + $"?sort={sort}&c=blonde"
                    },
                    new MenuItem()
                    {
                        title = "Большие жопы",
                        playlist_url = url + $"?sort={sort}&c=big-ass"
                    },
                    new MenuItem()
                    {
                        title = "Большие сиськи",
                        playlist_url = url + $"?sort={sort}&c=big-tits"
                    },
                    new MenuItem()
                    {
                        title = "Большие члены",
                        playlist_url = url + $"?sort={sort}&c=big-cock"
                    },
                    new MenuItem()
                    {
                        title = "Большой чёрный член",
                        playlist_url = url + $"?sort={sort}&c=bbc"
                    },
                    new MenuItem()
                    {
                        title = "Бондаж",
                        playlist_url = url + $"?sort={sort}&c=bondage"
                    },
                    new MenuItem()
                    {
                        title = "Босс",
                        playlist_url = url + $"?sort={sort}&c=boss"
                    },
                    new MenuItem()
                    {
                        title = "Бритые письки",
                        playlist_url = url + $"?sort={sort}&c=shaved-pussy"
                    },
                    new MenuItem()
                    {
                        title = "Брюнетки",
                        playlist_url = url + $"?sort={sort}&c=a1-brunette"
                    },
                    new MenuItem()
                    {
                        title = "Буккаке",
                        playlist_url = url + $"?sort={sort}&c=bukkake"
                    },
                    new MenuItem()
                    {
                        title = "В гольфах",
                        playlist_url = url + $"?sort={sort}&c=knee-socks"
                    },
                    new MenuItem()
                    {
                        title = "В клубе",
                        playlist_url = url + $"?sort={sort}&c=club"
                    },
                    new MenuItem()
                    {
                        title = "В красивом белье",
                        playlist_url = url + $"?sort={sort}&c=lingerie"
                    },
                    new MenuItem()
                    {
                        title = "В майке",
                        playlist_url = url + $"?sort={sort}&c=shirt"
                    },
                    new MenuItem()
                    {
                        title = "В масле",
                        playlist_url = url + $"?sort={sort}&c=oiled"
                    },
                    new MenuItem()
                    {
                        title = "В машине",
                        playlist_url = url + $"?sort={sort}&c=car-porn"
                    },
                    new MenuItem()
                    {
                        title = "В очках",
                        playlist_url = url + $"?sort={sort}&c=glasses"
                    },
                    new MenuItem()
                    {
                        title = "В презервативе",
                        playlist_url = url + $"?sort={sort}&c=condom"
                    },
                    new MenuItem()
                    {
                        title = "В спальне",
                        playlist_url = url + $"?sort={sort}&c=bedroom"
                    },
                    new MenuItem()
                    {
                        title = "В спортзале",
                        playlist_url = url + $"?sort={sort}&c=gym-porn"
                    },
                    new MenuItem()
                    {
                        title = "В чулках",
                        playlist_url = url + $"?sort={sort}&c=stockings"
                    },
                    new MenuItem()
                    {
                        title = "Вебкамера",
                        playlist_url = url + $"?sort={sort}&c=webcam"
                    },
                    new MenuItem()
                    {
                        title = "Волосатая пизда",
                        playlist_url = url + $"?sort={sort}&c=hairy"
                    },
                    new MenuItem()
                    {
                        title = "Гибкие",
                        playlist_url = url + $"?sort={sort}&c=flexible"
                    },
                    new MenuItem()
                    {
                        title = "Глотает сперму",
                        playlist_url = url + $"?sort={sort}&c=cum-swallow"
                    },
                    new MenuItem()
                    {
                        title = "Горничная",
                        playlist_url = url + $"?sort={sort}&c=maid"
                    },
                    new MenuItem()
                    {
                        title = "Госпожа",
                        playlist_url = url + $"?sort={sort}&c=mistress"
                    },
                    new MenuItem()
                    {
                        title = "Групповуха",
                        playlist_url = url + $"?sort={sort}&c=group-porno"
                    },
                    new MenuItem()
                    {
                        title = "Дилдо",
                        playlist_url = url + $"?sort={sort}&c=dildo"
                    },
                    new MenuItem()
                    {
                        title = "Длинные волосы",
                        playlist_url = url + $"?sort={sort}&c=long-hair"
                    },
                    new MenuItem()
                    {
                        title = "Доктор",
                        playlist_url = url + $"?sort={sort}&c=doctor"
                    },
                    new MenuItem()
                    {
                        title = "Домашнее порно",
                        playlist_url = url + $"?sort={sort}&c=amateur"
                    },
                    new MenuItem()
                    {
                        title = "Дрочит парню",
                        playlist_url = url + $"?sort={sort}&c=handjob"
                    },
                    new MenuItem()
                    {
                        title = "Евро",
                        playlist_url = url + $"?sort={sort}&c=a1-europe"
                    },
                    new MenuItem()
                    {
                        title = "Жесть",
                        playlist_url = url + $"?sort={sort}&c=fun"
                    },
                    new MenuItem()
                    {
                        title = "ЖМЖ",
                        playlist_url = url + $"?sort={sort}&c=a1-threesome"
                    },
                    new MenuItem()
                    {
                        title = "Измена",
                        playlist_url = url + $"?sort={sort}&c=cheating"
                    },
                    new MenuItem()
                    {
                        title = "Интимные стрижки",
                        playlist_url = url + $"?sort={sort}&c=intimate-haircut"
                    },
                    new MenuItem()
                    {
                        title = "Кляп в рот",
                        playlist_url = url + $"?sort={sort}&c=gag"
                    },
                    new MenuItem()
                    {
                        title = "Короткие волосы",
                        playlist_url = url + $"?sort={sort}&c=short-hair"
                    },
                    new MenuItem()
                    {
                        title = "Косички",
                        playlist_url = url + $"?sort={sort}&c=braids"
                    },
                    new MenuItem()
                    {
                        title = "Красивая грудь",
                        playlist_url = url + $"?sort={sort}&c=nice-tits-porn"
                    },
                    new MenuItem()
                    {
                        title = "Красивые",
                        playlist_url = url + $"?sort={sort}&c=a1-babe"
                    },
                    new MenuItem()
                    {
                        title = "Красивые попки",
                        playlist_url = url + $"?sort={sort}&c=ass"
                    },
                    new MenuItem()
                    {
                        title = "Красивый секс",
                        playlist_url = url + $"?sort={sort}&c=beautiful"
                    },
                    new MenuItem()
                    {
                        title = "Крупным планом",
                        playlist_url = url + $"?sort={sort}&c=closeup"
                    },
                    new MenuItem()
                    {
                        title = "Куколд",
                        playlist_url = url + $"?sort={sort}&c=cuckold"
                    },
                    new MenuItem()
                    {
                        title = "Куни",
                        playlist_url = url + $"?sort={sort}&c=cunni"
                    },
                    new MenuItem()
                    {
                        title = "Лесби",
                        playlist_url = url + $"?sort={sort}&c=lesbi-porno"
                    },
                    new MenuItem()
                    {
                        title = "Лижет попу",
                        playlist_url = url + $"?sort={sort}&c=ass-licking-porn"
                    },
                    new MenuItem()
                    {
                        title = "Массаж",
                        playlist_url = url + $"?sort={sort}&c=massage"
                    },
                    new MenuItem()
                    {
                        title = "Мастурбация",
                        playlist_url = url + $"?sort={sort}&c=a1-masturbation"
                    },
                    new MenuItem()
                    {
                        title = "Мачеха",
                        playlist_url = url + $"?sort={sort}&c=a1-stepmom"
                    },
                    new MenuItem()
                    {
                        title = "Медсестра",
                        playlist_url = url + $"?sort={sort}&c=nurse"
                    },
                    new MenuItem()
                    {
                        title = "Между сисек",
                        playlist_url = url + $"?sort={sort}&c=tits-fuck"
                    },
                    new MenuItem()
                    {
                        title = "Межрассовое",
                        playlist_url = url + $"?sort={sort}&c=interracial"
                    },
                    new MenuItem()
                    {
                        title = "МЖМ",
                        playlist_url = url + $"?sort={sort}&c=2man-woman"
                    },
                    new MenuItem()
                    {
                        title = "Минет",
                        playlist_url = url + $"?sort={sort}&c=blowjob"
                    },
                    new MenuItem()
                    {
                        title = "Молодые",
                        playlist_url = url + $"?sort={sort}&c=teen"
                    },
                    new MenuItem()
                    {
                        title = "На каблуках",
                        playlist_url = url + $"?sort={sort}&c=heels"
                    },
                    new MenuItem()
                    {
                        title = "На пляже",
                        playlist_url = url + $"?sort={sort}&c=beach"
                    },
                    new MenuItem()
                    {
                        title = "На природе",
                        playlist_url = url + $"?sort={sort}&c=outdoor-sex"
                    },
                    new MenuItem()
                    {
                        title = "На публике",
                        playlist_url = url + $"?sort={sort}&c=a1-public"
                    },
                    new MenuItem()
                    {
                        title = "На столе",
                        playlist_url = url + $"?sort={sort}&c=table"
                    },
                    new MenuItem()
                    {
                        title = "Наездница",
                        playlist_url = url + $"?sort={sort}&c=cowgirl"
                    },
                    new MenuItem()
                    {
                        title = "Наручники",
                        playlist_url = url + $"?sort={sort}&c=handcuffs"
                    },
                    new MenuItem()
                    {
                        title = "Натуральные сиськи",
                        playlist_url = url + $"?sort={sort}&c=a1-natural-tits"
                    },
                    new MenuItem()
                    {
                        title = "Негритянки",
                        playlist_url = url + $"?sort={sort}&c=black-girl"
                    },
                    new MenuItem()
                    {
                        title = "Негры",
                        playlist_url = url + $"?sort={sort}&c=black"
                    },
                    new MenuItem()
                    {
                        title = "Негры с блондинками",
                        playlist_url = url + $"?sort={sort}&c=blacks-on-blondes"
                    },
                    new MenuItem()
                    {
                        title = "Некрасивая грудь",
                        playlist_url = url + $"?sort={sort}&c=ugly-tits"
                    },
                    new MenuItem()
                    {
                        title = "Няня",
                        playlist_url = url + $"?sort={sort}&c=babysitter"
                    },
                    new MenuItem()
                    {
                        title = "Писает",
                        playlist_url = url + $"?sort={sort}&c=pissing"
                    },
                    new MenuItem()
                    {
                        title = "Плётка",
                        playlist_url = url + $"?sort={sort}&c=whip"
                    },
                    new MenuItem()
                    {
                        title = "Под водой",
                        playlist_url = url + $"?sort={sort}&c=underwater"
                    },
                    new MenuItem()
                    {
                        title = "Подчинение",
                        playlist_url = url + $"?sort={sort}&c=submission"
                    },
                    new MenuItem()
                    {
                        title = "Поза 69",
                        playlist_url = url + $"?sort={sort}&c=69"
                    },
                    new MenuItem()
                    {
                        title = "Порно зрелых",
                        playlist_url = url + $"?sort={sort}&c=milfs"
                    },
                    new MenuItem()
                    {
                        title = "Реслинг",
                        playlist_url = url + $"?sort={sort}&c=wrestling"
                    },
                    new MenuItem()
                    {
                        title = "Русское домашнее порно",
                        playlist_url = url + $"?sort={sort}&c=russian-amateur"
                    },
                    new MenuItem()
                    {
                        title = "Русское порно",
                        playlist_url = url + $"?sort={sort}&c=ruporn"
                    },
                    new MenuItem()
                    {
                        title = "Рыжие",
                        playlist_url = url + $"?sort={sort}&c=redhead"
                    },
                    new MenuItem()
                    {
                        title = "С латинками",
                        playlist_url = url + $"?sort={sort}&c=latina-sex"
                    },
                    new MenuItem()
                    {
                        title = "С невестой",
                        playlist_url = url + $"?sort={sort}&c=bride"
                    },
                    new MenuItem()
                    {
                        title = "С тренером",
                        playlist_url = url + $"?sort={sort}&c=couch-porn"
                    },
                    new MenuItem()
                    {
                        title = "Свингеры",
                        playlist_url = url + $"?sort={sort}&c=swingers"
                    },
                    new MenuItem()
                    {
                        title = "Секретарша",
                        playlist_url = url + $"?sort={sort}&c=secretary-porn"
                    },
                    new MenuItem()
                    {
                        title = "Секс в общаге",
                        playlist_url = url + $"?sort={sort}&c=dorm-porn"
                    },
                    new MenuItem()
                    {
                        title = "Секс в офисе",
                        playlist_url = url + $"?sort={sort}&c=office-sex"
                    },
                    new MenuItem()
                    {
                        title = "Секс на кухне",
                        playlist_url = url + $"?sort={sort}&c=kitchen"
                    },
                    new MenuItem()
                    {
                        title = "Секс с бывшей",
                        playlist_url = url + $"?sort={sort}&c=exgfs"
                    },
                    new MenuItem()
                    {
                        title = "Секс-игрушки",
                        playlist_url = url + $"?sort={sort}&c=sex-toys"
                    },
                    new MenuItem()
                    {
                        title = "Секс-машина",
                        playlist_url = url + $"?sort={sort}&c=sex-machines"
                    },
                    new MenuItem()
                    {
                        title = "Секс-рабыня",
                        playlist_url = url + $"?sort={sort}&c=slave"
                    },
                    new MenuItem()
                    {
                        title = "Силиконовые сиськи",
                        playlist_url = url + $"?sort={sort}&c=silicone-tits"
                    },
                    new MenuItem()
                    {
                        title = "Сквирт",
                        playlist_url = url + $"?sort={sort}&c=squirting"
                    },
                    new MenuItem()
                    {
                        title = "Соло",
                        playlist_url = url + $"?sort={sort}&c=a1-solo"
                    },
                    new MenuItem()
                    {
                        title = "Сперма вытекает",
                        playlist_url = url + $"?sort={sort}&c=creampie"
                    },
                    new MenuItem()
                    {
                        title = "Сперма на груди",
                        playlist_url = url + $"?sort={sort}&c=cum-on-tits"
                    },
                    new MenuItem()
                    {
                        title = "Сперма на лице",
                        playlist_url = url + $"?sort={sort}&c=facial"
                    },
                    new MenuItem()
                    {
                        title = "Сперма на ногах",
                        playlist_url = url + $"?sort={sort}&c=sperma-na-nogah"
                    },
                    new MenuItem()
                    {
                        title = "Сперма на пизде",
                        playlist_url = url + $"?sort={sort}&c=cum-on-pussy"
                    },
                    new MenuItem()
                    {
                        title = "Сперма на попе",
                        playlist_url = url + $"?sort={sort}&c=cum-on-ass"
                    },
                    new MenuItem()
                    {
                        title = "Старые с молодыми",
                        playlist_url = url + $"?sort={sort}&c=old-and-young"
                    },
                    new MenuItem()
                    {
                        title = "Страпон",
                        playlist_url = url + $"?sort={sort}&c=strapon"
                    },
                    new MenuItem()
                    {
                        title = "Стриптиз",
                        playlist_url = url + $"?sort={sort}&c=strip"
                    },
                    new MenuItem()
                    {
                        title = "Студентка",
                        playlist_url = url + $"?sort={sort}&c=schoolgirls"
                    },
                    new MenuItem()
                    {
                        title = "Студенты",
                        playlist_url = url + $"?sort={sort}&c=students"
                    },
                    new MenuItem()
                    {
                        title = "Стюардесса",
                        playlist_url = url + $"?sort={sort}&c=styuardessa"
                    },
                    new MenuItem()
                    {
                        title = "Трах",
                        playlist_url = url + $"?sort={sort}&c=trah"
                    },
                    new MenuItem()
                    {
                        title = "Учит трахаться",
                        playlist_url = url + $"?sort={sort}&c=teaching"
                    },
                    new MenuItem()
                    {
                        title = "Учитель",
                        playlist_url = url + $"?sort={sort}&c=teacher"
                    },
                    new MenuItem()
                    {
                        title = "Учительница",
                        playlist_url = url + $"?sort={sort}&c=teacher-milf"
                    },
                    new MenuItem()
                    {
                        title = "Футфетиш",
                        playlist_url = url + $"?sort={sort}&c=foot-fetish"
                    },
                    new MenuItem()
                    {
                        title = "Худые",
                        playlist_url = url + $"?sort={sort}&c=skinny-porn"
                    },
                    new MenuItem()
                    {
                        title = "Чешское порно",
                        playlist_url = url + $"?sort={sort}&c=czech-porn"
                    },
                    new MenuItem()
                    {
                        title = "Член из дырки",
                        playlist_url = url + $"?sort={sort}&c=gloryhole-porn"
                    },
                    new MenuItem()
                    {
                        title = "Эротика",
                        playlist_url = url + $"?sort={sort}&c=erotic"
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

        async public static ValueTask<StreamItem> StreamLinks(string uri, string host, string url, Func<string, ValueTask<string>> onresult, Func<string, ValueTask<string>> onlocation = null)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            string html = await onresult.Invoke($"{host}/{url}");
            if (html == null)
                return null;

            string stream_link = null;

            foreach (var item in new string[] { "video_url", "video_alt_url" })
            {
                stream_link = Regex.Match(html, "video_alt_url:([\t ]+)?('|\")(?<link>[^\"']+)").Groups["link"].Value;
                if (!string.IsNullOrEmpty(stream_link))
                    break;
            }

            if (string.IsNullOrEmpty(stream_link))
                return null;

            if (onlocation != null)
            {
                string location = await onlocation.Invoke(stream_link);
                if (location == null || stream_link == location || location.Contains("_file/"))
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

using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Model.Online;
using Shared.Model.Online.VDBmovies;
using Shared.Model.Templates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace JinEnergy.Online
{
    public class VDBmoviesController : BaseController
    {
        [JSInvokable("lite/vdbmovies")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.VDBmovies.Clone();

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int sid = int.Parse(parse_arg("sid", args) ?? "-1");

            if (arg.kinopoisk_id == 0)
                return EmptyError("arg");

            #region embed
            EmbedModel? embed = await InvokeCache(arg.id, $"cdnmoviesdb:json:{arg.kinopoisk_id}", async () =>
            {
                AppInit.JSRuntime?.InvokeAsync<object>("eval", "$('head meta[name=\"referrer\"]').attr('content', 'origin');");

                string? html = await JsHttpClient.Get($"{init.corsHost()}/kinopoisk/{arg.kinopoisk_id}/iframe", HeadersModel.Init(
                    ("Origin", "https://cdnmovies.net"),
                    ("Referer", "https://cdnmovies.net/"),
                    ("User-Agent", "Mozilla/5.0 (Linux; Android 10; K; client) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.6167.178 Mobile Safari/537.36")
                ));

                AppInit.JSRuntime?.InvokeAsync<object>("eval", "$('head meta[name=\"referrer\"]').attr('content', 'no-referrer');");

                string file = Regex.Match(html ?? "", "&quot;player&quot;:&quot;(#[^&]+)").Groups[1].Value;
                if (string.IsNullOrEmpty(file))
                    return null;

                try
                {
                    string? json = await JSRuntime.InvokeAsync<string?>("eval", @"(function () {
                          var enc = function enc(str) {
	                        return btoa(encodeURIComponent(str).replace(/%([0-9A-F]{2})/g, function (match, p1) {
	                          return String.fromCharCode('0x' + p1);
	                        }));
                          };

                          var dec = function dec(str) {
	                        return decodeURIComponent(atob(str).split('').map(function (c) {
	                          return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
	                        }).join(''));
                          };

                          var trashList = ['wNp2wBTNcPRQvTC0_CpxCsq_8T1u9Q', 'md-Od2G9RWOgSa5HoBSSbWrCyIqQyY', 'kzuOYQqB_QSOL-xzN_Kz3kkgkHhHit', '6-xQWMh7ertLp8t_M9huUDk1M0VrYJ', 'RyTwtf15_GLEsXxnpU4Ljjd0ReY-VH'];
                          var x = '" + file + @"'.substring(2);
                          trashList.forEach(function (trash) {
	                        x = x.replace('//' + enc(trash), '');
                          });

                          try {
	                        x = dec(x);
                          } catch (e) {
	                        x = '';
                          }

                          return x;
                        })();
                    ");

                    if (string.IsNullOrEmpty(json))
                        return null;

                    if (json.Contains("\"folder\""))
                    {
                        var serial = JsonSerializer.Deserialize<List<Lampac.Models.LITE.CDNmovies.Voice>>(json);
                        if (serial == null || serial.Count == 0)
                            return null;

                        return new EmbedModel() { serial = serial };
                    }
                    else
                    {
                        var movies = JsonSerializer.Deserialize<List<Episode>>(json);
                        if (movies == null || movies.Count == 0)
                            return null;

                        return new EmbedModel() { movies = movies };
                    }
                }
                catch
                {
                    return null;
                }
            });

            if (embed == null)
                return EmptyError("embed");
            #endregion

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            if (embed.movies != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(arg.title, arg.original_title, embed.movies.Count);

                foreach (var m in embed.movies)
                {
                    #region subtitle
                    var subtitles = new SubtitleTpl();

                    if (!string.IsNullOrEmpty(m.subtitle))
                    {
                        var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(m.subtitle);
                        while (match.Success)
                        {
                            subtitles.Append(match.Groups[1].Value, match.Groups[2].Value);
                            match = match.NextMatch();
                        }
                    }
                    #endregion

                    if (string.IsNullOrEmpty(m.file))
                        continue;

                    string file = Regex.Matches(m.file, "\\](https?://[^\\[\\|,\n\r\t ]+\\.m3u8)").Reverse().First().Groups[1].Value;
                    //file = file.Replace("sundb.coldcdn.xyz", "sundb.nl");
                    file = file.Replace(":hls:manifest.m3u8", "");

                    if (string.IsNullOrEmpty(file))
                        continue;

                    mtpl.Append(m.title, file, subtitles: subtitles);
                }

                return mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                if (s == -1)
                {
                    #region Сезоны
                    string? enc_title = HttpUtility.UrlEncode(arg.title);
                    string? enc_original_title = HttpUtility.UrlEncode(arg.original_title);

                    for (int i = 0; i < embed.serial.Count; i++)
                    {
                        string season = Regex.Match(embed.serial[i].title, "^([0-9]+)").Groups[1].Value;
                        if (string.IsNullOrEmpty(season))
                            continue;

                        string link = $"lite/vdbmovies?serial=1&kinopoisk_id={arg.kinopoisk_id}&imdb_id={arg.imdb_id}&title={enc_title}&original_title={enc_original_title}&s={season}&sid={i}";

                        html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season} сезон" + "</div></div></div>");
                        firstjson = false;
                    }
                    #endregion
                }
                else
                {
                    #region Серии
                    foreach (var item in embed.serial[sid].folder)
                    {
                        string episode = Regex.Match(item.title, "^([0-9]+)").Groups[1].Value;

                        string file = Regex.Matches(item.folder[0].file, "\\](https?://[^\\[\\|,\n\r\t ]+\\.m3u8)").Reverse().First().Groups[1].Value;
                        //file = file.Replace("sundb.coldcdn.xyz", "sundb.nl");
                        file = file.Replace(":hls:manifest.m3u8", "");

                        html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode + "\" data-json='{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + $"{arg.title ?? arg.original_title} ({episode} cерия)" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode} cерия" + "</div></div>");
                        firstjson = false;
                    }
                    #endregion
                }
                #endregion
            }

            return html.ToString() + "</div>";
        }
    }
}

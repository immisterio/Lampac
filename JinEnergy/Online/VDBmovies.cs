using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Model.Online.VDBmovies;
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
            var arg = defaultArgs(args);
            int serial = int.Parse(parse_arg("serial", args) ?? "-1");
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int sid = int.Parse(parse_arg("sid", args) ?? "-1");

            if (serial == -1 || (string.IsNullOrEmpty(arg.imdb_id) && arg.kinopoisk_id == 0))
                return EmptyError("arg");

            #region iframe_src
            //string? iframe_src = await InvokeCache(arg.id, $"cdnmoviesdb:iframe_src:{arg.imdb_id}:{arg.kinopoisk_id}", async () => 
            //{
            //    string uri = $"{AppInit.VDBmovies.corsHost()}/api/short?token={AppInit.VDBmovies.token}&kinopoisk_id={arg.kinopoisk_id}&imdb_id={arg.imdb_id}";
            //    var root = await JsHttpClient.Get<RootObject>(uri);
            //    if (root?.data == null || root.data.Count == 0)
            //        return null;

            //    return root.data.First().Value?.iframe_src;
            //});

            //if (string.IsNullOrEmpty(iframe_src))
            //    return OnError("iframe_src");
            #endregion

            #region embed
            EmbedModel? embed = await InvokeCache(arg.id, $"cdnmoviesdb:json:{arg.imdb_id}:{arg.kinopoisk_id}", async () =>
            {
                string? html = await JsHttpClient.Get($"{AppInit.VDBmovies.corsHost()}/kinopoisk/{arg.kinopoisk_id}/iframe", addHeaders: new List<(string name, string val)>()
                {
                    ("Origin", "https://cdnmovies.net"),
                    ("Referer", "https://cdnmovies.net/")
                });

                string file = Regex.Match(html ?? "", "file: ?'(#[^']+)'").Groups[1].Value;
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

                          var trashList = ['-*frofpscprpamfpQ*45612.3256dfrgd', '54vjfhcgdbrydkcfkndz568436fred+*d', 'lvfycgndqcydrcgcfg+95147gfdgf-zd*', 'az+-erw*3457edgtjd-feqsptf/re*q*Y', 'df8vg69r9zxWdlyf+*fgx455g8fh9z-e*Q'];
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

                    if (serial == 0)
                    {
                        var movies = JsonSerializer.Deserialize<List<Episode>>(json);
                        if (movies == null || movies.Count == 0)
                            return null;

                        return new EmbedModel() { movies = movies };
                    }
                    else
                    {
                        var serial = JsonSerializer.Deserialize<List<Lampac.Models.LITE.CDNmovies.Voice>>(json);
                        if (serial == null || serial.Count == 0)
                            return null;

                        return new EmbedModel() { serial = serial };
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
                foreach (var m in embed.movies)
                {
                    #region subtitle
                    string subtitles = string.Empty;

                    if (!string.IsNullOrEmpty(m.subtitle))
                    {
                        var subbuild = new StringBuilder();
                        var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(m.subtitle);
                        while (match.Success)
                        {
                            if (!string.IsNullOrEmpty(match.Groups[1].Value) && !string.IsNullOrEmpty(match.Groups[2].Value))
                                subbuild.Append("{\"label\": \"" + match.Groups[1].Value + "\",\"url\": \"" + match.Groups[2].Value + "\"},");

                            match = match.NextMatch();
                        }

                        if (subbuild.Length > 0)
                            subtitles = Regex.Replace(subbuild.ToString(), ",$", "");
                    }
                    #endregion

                    if (string.IsNullOrEmpty(m.file))
                        continue;

                    string file = Regex.Matches(m.file, "(https?://[^\\[\\|,\n\r\t ]+\\.m3u8)").Reverse().First().Groups[1].Value;
                    file = file.Replace("sundb.coldcdn.xyz", "sundb.nl");
                    //file = Regex.Replace(file, "/[^/]+$", "/hls.m3u8");

                    if (string.IsNullOrEmpty(file))
                        continue;

                    html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + (arg.title ?? arg.original_title) + "\", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + m.title + "</div></div>");
                    firstjson = false;
                }
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

                        string file = Regex.Matches(item.folder[0].file, "(https?://[^\\[\\|,\n\r\t ]+\\.m3u8)").Reverse().First().Groups[1].Value;
                        file = file.Replace("sundb.coldcdn.xyz", "sundb.nl");
                        //file = Regex.Replace(file, "/[^/]+$", "/hls.m3u8");

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

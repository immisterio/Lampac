using Shared.Model.Online.VDBmovies;
using Shared.Model.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class VDBmoviesInvoke
    {
        #region VDBmoviesInvoke
        string? host;
        bool usehls;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public VDBmoviesInvoke(string? host, bool hls, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            usehls = hls;
        }
        #endregion

        #region EvalCode
        public string EvalCode(string file)
        {
            return @"(function () {
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
            ";
        }
        #endregion

        #region Embed
        public EmbedModel? Embed(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            string quality = json.Contains("1080p") ? "1080p" : json.Contains("720p") ? "720p" : json.Contains("480p") ? "480p" : "360p";

            if (json.Contains("\"folder\""))
            {
                var serial = JsonSerializer.Deserialize<List<Lampac.Models.LITE.CDNmovies.Voice>>(json);
                if (serial == null || serial.Count == 0)
                    return null;

                return new EmbedModel() { serial = serial, quality = quality };
            }
            else
            {
                var movies = JsonSerializer.Deserialize<List<Episode>>(json);
                if (movies == null || movies.Count == 0)
                    return null;

                return new EmbedModel() { movies = movies, quality = quality };
            }
        }
        #endregion

        #region Html
        public string Html(EmbedModel? root, long kinopoisk_id, string? title, string? original_title, string? t, int s, int sid)
        {
            if (root == null)
                return string.Empty;

            if (root.movies != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, root.movies.Count);

                foreach (var m in root.movies)
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

                    var streams = new List<(string link, string quality)>() { Capacity = 5 };
                    foreach (Match mf in Regex.Matches(m.file, "\\[([^\\]]+)\\](https?://[^\\[\\|,\n\r\t ]+\\.m3u8)"))
                    {
                        string link = mf.Groups[2].Value;
                        if (!usehls)
                            link = link.Replace(":hls:manifest.m3u8", "");

                        streams.Insert(0, (onstreamfile.Invoke(link), mf.Groups[1].Value));
                    }

                    mtpl.Append(m.title, streams[0].link, subtitles: subtitles, streamquality: new StreamQualityTpl(streams));
                }

                return mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                string? enc_title = HttpUtility.UrlEncode(title);
                string? enc_original_title = HttpUtility.UrlEncode(original_title);

                if (s == -1)
                {
                    #region Сезоны
                    var tpl = new SeasonTpl(root.quality);

                    for (int i = 0; i < root.serial.Count; i++)
                    {
                        string season = Regex.Match(root.serial[i].title, "^([0-9]+)").Groups[1].Value;
                        if (string.IsNullOrEmpty(season))
                            continue;

                        tpl.Append($"{season} сезон", host + $"lite/vdbmovies?kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&s={season}&sid={i}");
                    }

                    return tpl.ToHtml();
                    #endregion
                }
                else
                {
                    #region Серии
                    var vtpl = new VoiceTpl();
                    var etpl = new EpisodeTpl();

                    var hashvoices = new HashSet<string>();

                    foreach (var episode in root.serial[sid].folder)
                    {
                        string ename = Regex.Match(episode.title, "^([0-9]+)").Groups[1].Value;

                        foreach (var voice in episode.folder)
                        {
                            string perevod = voice.title;
                            if (string.IsNullOrEmpty(t))
                                t = perevod;

                            if (!hashvoices.Contains(perevod))
                            {
                                hashvoices.Add(perevod);
                                vtpl.Append(perevod, t == perevod, host + $"lite/vdbmovies?kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&s={s}&sid={sid}&t={HttpUtility.UrlEncode(perevod)}");
                            }

                            if (perevod != t)
                                continue;

                            var streams = new List<(string link, string quality)>() { Capacity = 5 };
                            foreach (Match mf in Regex.Matches(voice.file, "\\[([^\\]]+)\\](https?://[^\\[\\|,\n\r\t ]+\\.m3u8)"))
                            {
                                string link = mf.Groups[2].Value;
                                if (!usehls)
                                    link = link.Replace(":hls:manifest.m3u8", "");

                                streams.Insert(0, (onstreamfile.Invoke(link), mf.Groups[1].Value));
                            }

                            etpl.Append($"{ename} cерия", $"{title ?? original_title} ({ename} cерия)", s.ToString(), ename, streams[0].link, streamquality: new StreamQualityTpl(streams));
                        }
                    }

                    return vtpl.ToHtml() + etpl.ToHtml();
                    #endregion
                }
                #endregion
            }
        }
        #endregion
    }
}

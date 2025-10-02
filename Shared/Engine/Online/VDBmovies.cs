using Shared.Models.Base;
using Shared.Models.Online.VDBmovies;
using Shared.Models.Templates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public struct VDBmoviesInvoke
    {
        #region VDBmoviesInvoke
        string host;
        bool usehls;
        Func<string, string> onstreamfile;
        Func<string, string> onlog;

        public VDBmoviesInvoke(string host, bool hls, Func<string, string> onstreamfile, Func<string, string> onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            usehls = hls;
        }
        #endregion

        #region EvalCode
        public string EvalCode(in string file)
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

        #region DecodeEval
        public string DecodeEval(in string file)
        {
            Func<string, string> enc = str =>
            {
                var bytes = Encoding.UTF8.GetBytes(str);
                return Convert.ToBase64String(bytes);
            };

            Func<string, string> dec = str =>
            {
                var bytes = Convert.FromBase64String(str);
                return Encoding.UTF8.GetString(bytes);
            };

            List<string> trashList = new List<string>
            {
                "wNp2wBTNcPRQvTC0_CpxCsq_8T1u9Q",
                "md-Od2G9RWOgSa5HoBSSbWrCyIqQyY",
                "kzuOYQqB_QSOL-xzN_Kz3kkgkHhHit",
                "6-xQWMh7ertLp8t_M9huUDk1M0VrYJ",
                "RyTwtf15_GLEsXxnpU4Ljjd0ReY-VH"
            };

            string x = file.Substring(2);

            foreach (var trash in trashList)
                x = x.Replace("//" + enc(trash), "");

            try
            {
                x = dec(x);
            }
            catch
            {
                x = string.Empty;
            }

            return x;
        }
        #endregion

        #region Embed
        public EmbedModel Embed(in string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            string quality = json.Contains("1080p") ? "1080p" : json.Contains("720p") ? "720p" : json.Contains("480p") ? "480p" : "360p";

            try
            {
                if (json.Contains("\"folder\""))
                {
                    var serial = JsonSerializer.Deserialize<Models.Online.CDNmovies.Voice[]>(json);
                    if (serial == null || serial.Length == 0)
                        return null;

                    return new EmbedModel() { serial = serial, quality = quality };
                }
                else
                {
                    var movies = JsonSerializer.Deserialize<Episode[]>(json);
                    if (movies == null || movies.Length == 0)
                        return null;

                    return new EmbedModel() { movies = movies, quality = quality };
                }
            }
            catch { return null; }
        }
        #endregion

        #region Html
        public string Html(EmbedModel root, string orid, string imdb_id, long kinopoisk_id, string title, string original_title, string t, int s, int sid, VastConf vast = null, bool rjson = false)
        {
            if (root == null)
                return string.Empty;

            if (root.movies != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, root.movies.Length);

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

                    var streamquality = new StreamQualityTpl();

                    foreach (Match mf in Regex.Matches(m.file, "\\[([^\\]]+)\\](https?://[^\\[\\|,\n\r\t ]+\\.m3u8)"))
                    {
                        string link = mf.Groups[2].Value;
                        if (!usehls)
                            link = link.Replace(":hls:manifest.m3u8", "");

                        streamquality.Insert(onstreamfile.Invoke(link), mf.Groups[1].Value);
                    }

                    mtpl.Append(m.title, streamquality.Firts().link, subtitles: subtitles, streamquality: streamquality, vast: vast);
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);

                if (s == -1)
                {
                    #region Сезоны
                    var tpl = new SeasonTpl(root.quality, root.serial.Length);

                    for (int i = 0; i < root.serial.Length; i++)
                    {
                        string season = Regex.Match(root.serial[i].title, "^([0-9]+)").Groups[1].Value;
                        if (string.IsNullOrEmpty(season))
                            continue;

                        tpl.Append($"{season} сезон", host + $"lite/vdbmovies?orid={orid}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&rjson={rjson}&title={enc_title}&original_title={enc_original_title}&s={season}&sid={i}", season);
                    }

                    return rjson ? tpl.ToJson() : tpl.ToHtml();
                    #endregion
                }
                else
                {
                    #region Серии
                    var vtpl = new VoiceTpl();
                    var etpl = new EpisodeTpl();

                    var hashvoices = new HashSet<string>();

                    string sArhc = s.ToString();

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
                                vtpl.Append(perevod, t == perevod, host + $"lite/vdbmovies?orid={orid}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&rjson={rjson}&title={enc_title}&original_title={enc_original_title}&s={s}&sid={sid}&t={HttpUtility.UrlEncode(perevod)}");
                            }

                            if (perevod != t)
                                continue;

                            var streamquality = new StreamQualityTpl();

                            foreach (Match mf in Regex.Matches(voice.file, "\\[([^\\]]+)\\](https?://[^\\[\\|,\n\r\t ]+\\.m3u8)"))
                            {
                                string link = mf.Groups[2].Value;
                                if (!usehls)
                                    link = link.Replace(":hls:manifest.m3u8", "");

                                streamquality.Insert(onstreamfile.Invoke(link), mf.Groups[1].Value);
                            }

                            etpl.Append($"{ename} cерия", title ?? original_title, sArhc, ename, streamquality.Firts().link, streamquality: streamquality, vast: vast);
                        }
                    }

                    if (rjson)
                        return etpl.ToJson(vtpl);

                    return vtpl.ToHtml() + etpl.ToHtml();
                    #endregion
                }
                #endregion
            }
        }
        #endregion
    }
}

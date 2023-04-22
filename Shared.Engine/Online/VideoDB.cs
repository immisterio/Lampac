using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class VideoDBInvoke
    {
        #region VideoDBInvoke
        string? host;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;
        Func<string, List<(string name, string val)>?, ValueTask<string?>> onget;

        public VideoDBInvoke(string? host, Func<string, List<(string name, string val)>?, ValueTask<string?>> onget, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.onget = onget;
        }
        #endregion

        #region Embed
        public async ValueTask<(JsonArray pl, bool movie)> Embed(long kinopoisk_id, int serial)
        {
            if (kinopoisk_id == 0)
                return default;

            string host = "https://kinoplay.site";

            string? html = await onget.Invoke($"{host}/iplayer/videodb.php?kp={kinopoisk_id}" + (serial > 0 ? "&series=true" : ""), new List<(string name, string val)>()
            {
                ("cookie", "invite=a246a3f46c82fe439a45c3dbbbb24ad5"),
                ("referer", $"{host}/")
            });

            onlog?.Invoke(html ?? "html null");

            string? file = new Regex("file:([^\n\r]+,\\])").Match(html ?? "").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(file))
                return default;

            file = Regex.Replace(file.Trim(), "(\\{|, )([a-z]+):", "$1\"$2\":")
                        .Replace("\": \"", "\":\"")
                        .Replace("},]", "}]");

            onlog?.Invoke("file: " + file);
            var pl = JsonSerializer.Deserialize<JsonArray>(file);
            if (pl == null) 
                return default;

            onlog?.Invoke("pl " + pl.Count);
            return (pl, !file.Contains("\"comment\":"));
        }
        #endregion

        #region Html
        public string Html((JsonArray pl, bool movie) root, long kinopoisk_id, string? title, string? original_title, string? t, int s, int sid)
        {
            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (root.movie)
            {
                #region Фильм
                foreach (var pl in root.pl)
                {
                    string? name = pl?["title"]?.GetValue<string>();
                    string? file = pl?["file"]?.GetValue<string>();

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                        continue;

                    #region streansquality
                    string streansquality = string.Empty;
                    List<(string link, string quality)> streams = new List<(string, string)>();

                    foreach (var quality in new List<string> { /*"2160", "2060", "1440",*/ "1080", "720", "480", "360" })
                    {
                        string link = new Regex($"\\[{quality}p?\\]" + "([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))").Match(file).Groups[1].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        link = onstreamfile.Invoke(link);

                        streams.Add((link, $"{quality}p"));
                        streansquality += $"\"{quality}p\":\"" + link + "\",";
                    }

                    streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";
                    #endregion

                    #region subtitle
                    string subtitles = string.Empty;

                    try
                    {
                        int subx = 1;
                        foreach (string cc in pl["subtitle"].GetValue<string>().Split(","))
                        {
                            if (string.IsNullOrWhiteSpace(cc) || !cc.EndsWith(".srt"))
                                continue;

                            string suburl = onstreamfile.Invoke(cc);
                            subtitles += "{\"label\": \"" + $"sub #{subx}" + "\",\"url\": \"" + suburl + "\"},";
                            subx++;
                        }
                    }
                    catch { }

                    subtitles = Regex.Replace(subtitles, ",$", "");
                    #endregion


                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + (title ?? original_title) + "\", " + streansquality + ", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + name + "</div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Сериал
                if (s == -1)
                {
                    for (int i = 0; i < root.pl.Count; i++)
                    {
                        string name = root.pl[i]["title"].GetValue<string>();
                        string season = Regex.Match(name, "^([0-9]+)").Groups[1].Value;
                        string link = host + $"lite/videodb?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={season}&sid={i}";

                        html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + name + "</div></div></div>";
                        firstjson = false;
                    }
                }
                else
                {
                    var season = root.pl?[sid]?["folder"]?.AsArray();
                    if (season == null)
                        return string.Empty;

                    #region Перевод
                    foreach (var episode in season)
                    {
                        var episodes = episode?["folder"]?.AsArray();
                        if (episodes == null)
                            continue;

                        foreach (var pl in episodes)
                        {
                            string? perevod = pl?["comment"]?.GetValue<string>();
                            if (perevod == null)
                                continue;

                            if (html.Contains(perevod))
                                continue;

                            if (string.IsNullOrWhiteSpace(t))
                                t = perevod;

                            string link = host + $"lite/videodb?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={s}&sid={sid}&t={HttpUtility.UrlEncode(perevod)}";
                            string active = t == perevod ? "active" : "";

                            html += "<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + perevod + "</div>";
                        }
                    }

                    html += "</div><div class=\"videos__line\">";
                    #endregion

                    #region Серии
                    foreach (var episode in season)
                    {
                        var episodes = episode?["folder"]?.AsArray();
                        if (episodes == null)
                            continue;

                        foreach (var pl in episodes)
                        {
                            string? perevod = pl?["comment"]?.GetValue<string>();
                            if (perevod != t)
                                continue;

                            string? name = episode?["title"]?.GetValue<string>();
                            string? file = pl?["file"]?.GetValue<string>();

                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                                continue;

                            string streansquality = string.Empty;
                            List<(string link, string quality)> streams = new List<(string, string)>();

                            foreach (var quality in new List<string> { /*"2160", "2060", "1440",*/ "1080", "720", "480", "360" })
                            {
                                string link = new Regex($"\\[{quality}p?\\]" + "([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))").Match(file).Groups[1].Value;
                                if (string.IsNullOrEmpty(link))
                                    continue;

                                link = onstreamfile.Invoke(link);

                                streams.Add((link, $"{quality}p"));
                                streansquality += $"\"{quality}p\":\"" + link + "\",";
                            }

                            streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                            html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(name, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + $"{title ?? original_title} ({name})" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + name + "</div></div>";
                            firstjson = false;
                        }
                    }
                    #endregion
                }
                #endregion
            }

            return html + "</div>";
        }
        #endregion
    }
}

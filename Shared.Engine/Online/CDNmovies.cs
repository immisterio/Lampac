using Lampac.Models.LITE.CDNmovies;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class CDNmoviesInvoke
    {
        #region CDNmoviesInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string> onstreamfile;

        public CDNmoviesInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string> onstreamfile)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
        }
        #endregion

        #region Embed
        public async ValueTask<List<Voice>?> Embed(long kinopoisk_id)
        {
            if(kinopoisk_id == 0)
                return null;

            string? html = await onget.Invoke($"{apihost}/serial/kinopoisk/{kinopoisk_id}");
            if (html == null)
                return null;

            string file = Regex.Match(html, "file:'([^\n\r]+)'").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(file))
                return null;

            List<Voice>? content;

            try
            {
                content = JsonSerializer.Deserialize<List<Voice>>(file);
            }
            catch { return null; }

            if (content == null || content.Count == 0)
                return null;

            return content;
        }
        #endregion

        #region Html
        public string Html(List<Voice> voices, long kinopoisk_id, string? title, string? original_title, int t, int s, int sid)
        {
            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            #region Перевод html
            for (int i = 0; i < voices.Count; i++)
            {
                string link = host + $"lite/cdnmovies?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={i}";

                html += "<div class=\"videos__button selector " + (t == i ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + voices[i].title + "</div>";
            }

            html += "</div><div class=\"videos__line\">";
            #endregion

            if (s == -1)
            {
                #region Сезоны
                for (int i = 0; i < voices[t].folder.Count; i++)
                {
                    string season = Regex.Match(voices[t].folder[i].title, "([0-9]+)$").Groups[1].Value;
                    string link = host + $"lite/cdnmovies?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={t}&s={season}&sid={i}";

                    html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season} сезон" + "</div></div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Серии
                foreach (var item in voices[t].folder[sid].folder)
                {
                    string streansquality = string.Empty;
                    List<(string link, string quality)> streams = new List<(string, string)>();

                    foreach (var quality in new List<string> { "720", "480", "360", "240" })
                    {
                        string link = new Regex($"\\[{quality}p?\\]" + "([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))").Match(item.file).Groups[1].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        link = onstreamfile.Invoke(link);

                        streams.Add((link, $"{quality}p"));
                        streansquality += $"\"{quality}p\":\"" + link + "\",";
                    }

                    streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                    string episode = Regex.Match(item.title, "([0-9]+)$").Groups[1].Value;
                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + $"{title ?? original_title} ({episode} cерия)" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode} cерия" + "</div></div>";
                    firstjson = false;
                }
                #endregion
            }

            return html + "</div>";
        }
        #endregion
    }
}

using Lampac.Models.LITE.CDNmovies;
using System.Text;
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
        Action? requesterror;

        public CDNmoviesInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string> onstreamfile, Action? requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        public async ValueTask<List<Voice>?> Embed(long kinopoisk_id)
        {
            string? html = await onget.Invoke($"{apihost}/serial/kinopoisk/{kinopoisk_id}");
            if (html == null)
            {
                requesterror?.Invoke();
                return null;
            }

            string file = Regex.Match(html, "file:'([^\n\r]+)'").Groups[1].Value;
            if (string.IsNullOrEmpty(file))
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
        public string Html(List<Voice>? voices, long kinopoisk_id, string? title, string? original_title, int t, int s, int sid)
        {
            if (voices == null || voices.Count == 0)
                return string.Empty;

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            #region Перевод html
            for (int i = 0; i < voices.Count; i++)
            {
                string link = host + $"lite/cdnmovies?kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&t={i}";

                html.Append("<div class=\"videos__button selector " + (t == i ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + voices[i].title + "</div>");
            }

            html.Append("</div><div class=\"videos__line\">");
            #endregion

            if (s == -1)
            {
                #region Сезоны
                for (int i = 0; i < voices[t].folder.Count; i++)
                {
                    string season = Regex.Match(voices[t].folder[i].title, "([0-9]+)$").Groups[1].Value;
                    if (string.IsNullOrEmpty(season))
                        continue;

                    string link = host + $"lite/cdnmovies?kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&t={t}&s={season}&sid={i}";

                    html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season} сезон" + "</div></div></div>");
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Серии
                foreach (var item in voices[t].folder[sid].folder)
                {
                    var streams = new List<(string link, string quality)>() { Capacity = 2 };

                    foreach (Match m in Regex.Matches(item.file, "\\[(360|240)p?\\]([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))"))
                    {
                        string link = m.Groups[2].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        streams.Insert(0, (onstreamfile.Invoke(link), $"{m.Groups[1].Value}p"));
                    }

                    if (streams.Count == 0)
                        continue;

                    string streansquality = "\"quality\": {" + string.Join(",", streams.Select(s => $"\"{s.quality}\":\"{s.link}\"")) + "}";

                    string episode = Regex.Match(item.title, "([0-9]+)$").Groups[1].Value;
                    html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + $"{title ?? original_title} ({episode} cерия)" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode} cерия" + "</div></div>");
                    firstjson = false;
                }
                #endregion
            }

            return html.ToString() + "</div>";
        }
        #endregion
    }
}

using Lampac.Models.LITE.CDNmovies;
using Shared.Model.Templates;
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
        public string Html(List<Voice>? voices, long kinopoisk_id, string? title, string? original_title, int t, int s, int sid, bool rjson = false)
        {
            if (voices == null || voices.Count == 0)
                return string.Empty;

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            #region Перевод html
            var vtpl = new VoiceTpl();

            for (int i = 0; i < voices.Count; i++)
            {
                string link = host + $"lite/cdnmovies?rjson={rjson}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&t={i}";
                vtpl.Append(voices[i].title, t == i, link);
            }
            #endregion

            if (s == -1)
            {
                #region Сезоны
                var tpl = new SeasonTpl(voices[t].folder.Count);

                for (int i = 0; i < voices[t].folder.Count; i++)
                {
                    string season = Regex.Match(voices[t].folder[i].title, "([0-9]+)$").Groups[1].Value;
                    if (string.IsNullOrEmpty(season))
                        continue;

                    string link = host + $"lite/cdnmovies?rjson={rjson}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&t={t}&s={season}&sid={i}";
                    tpl.Append($"{season} сезон", link, season);
                }

                return rjson ? tpl.ToJson(vtpl) : (vtpl.ToHtml() + tpl.ToHtml());
                #endregion
            }
            else
            {
                #region Серии
                var etpl = new EpisodeTpl();

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

                    string episode = Regex.Match(item.title, "([0-9]+)$").Groups[1].Value;
                    etpl.Append($"{episode} cерия", title ?? original_title, s.ToString(), episode, streams[0].link, streamquality: new StreamQualityTpl(streams));
                }

                if (rjson)
                    return etpl.ToJson(vtpl);

                return vtpl.ToHtml() + etpl.ToHtml();
                #endregion
            }
        }
        #endregion
    }
}

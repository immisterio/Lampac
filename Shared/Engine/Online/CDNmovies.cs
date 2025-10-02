using Shared.Models.Base;
using Shared.Models.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Shared.Models.Online.CDNmovies;

namespace Shared.Engine.Online
{
    public struct CDNmoviesInvoke
    {
        #region CDNmoviesInvoke
        string host;
        string apihost;
        Func<string, ValueTask<string>> onget;
        Func<string, string> onstreamfile;
        Action requesterror;

        public CDNmoviesInvoke(string host, string apihost, Func<string, ValueTask<string>> onget, Func<string, string> onstreamfile, Action requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        public async ValueTask<Voice[]> Embed(long kinopoisk_id)
        {
            string html = await onget.Invoke($"{apihost}/serial/kinopoisk/{kinopoisk_id}");
            if (html == null)
            {
                requesterror?.Invoke();
                return null;
            }

            string file = Regex.Match(html, "file:'([^\n\r]+)'").Groups[1].Value;
            if (string.IsNullOrEmpty(file))
                return null;

            Voice[] content;

            try
            {
                content = JsonSerializer.Deserialize<Voice[]>(file);
            }
            catch { return null; }

            if (content == null || content.Length == 0)
                return null;

            return content;
        }
        #endregion

        #region Html
        public string Html(Voice[] voices, long kinopoisk_id, string title, string original_title, int t, int s, int sid, VastConf vast = null, bool rjson = false)
        {
            if (voices == null || voices.Length == 0)
                return string.Empty;

            string enc_title = HttpUtility.UrlEncode(title);
            string enc_original_title = HttpUtility.UrlEncode(original_title);

            #region Перевод html
            var vtpl = new VoiceTpl(voices.Length);

            for (int i = 0; i < voices.Length; i++)
            {
                string link = host + $"lite/cdnmovies?rjson={rjson}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&t={i}";
                vtpl.Append(voices[i].title, t == i, link);
            }
            #endregion

            if (s == -1)
            {
                #region Сезоны
                var tpl = new SeasonTpl(voices[t].folder.Length);

                for (int i = 0; i < voices[t].folder.Length; i++)
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
                string sArhc = s.ToString();

                foreach (var item in voices[t].folder[sid].folder)
                {
                    var streamquality = new StreamQualityTpl();

                    foreach (Match m in Regex.Matches(item.file, "\\[(360|240)p?\\]([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))"))
                    {
                        string link = m.Groups[2].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        streamquality.Insert(onstreamfile.Invoke(link), $"{m.Groups[1].Value}p");
                    }

                    string episode = Regex.Match(item.title, "([0-9]+)$").Groups[1].Value;
                    etpl.Append($"{episode} cерия", title ?? original_title, sArhc, episode, streamquality.Firts().link, streamquality: streamquality, vast: vast);
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

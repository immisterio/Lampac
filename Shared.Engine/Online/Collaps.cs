using Lampac.Models.LITE.Collaps;
using Shared.Model.Online.Collaps;
using Shared.Model.Templates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class CollapsInvoke
    {
        #region CollapsInvoke
        string? host;
        string apihost;
        bool dash;
        Func<string, ValueTask<string?>> onget;
        Func<string, string> onstreamfile;
        Action? requesterror;

        public CollapsInvoke(string? host, string apihost, bool dash, Func<string, ValueTask<string?>> onget, Func<string, string> onstreamfile, Action? requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.dash = dash;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        public async ValueTask<EmbedModel?> Embed(string? imdb_id, long kinopoisk_id)
        {
            string uri = $"{apihost}/embed/imdb/{imdb_id}";
            if (kinopoisk_id > 0)
                uri = $"{apihost}/embed/kp/{kinopoisk_id}";

            string? content = await onget.Invoke(uri);
            if (string.IsNullOrEmpty(content))
            {
                requesterror?.Invoke();
                return null;
            }

            if (!content.Contains("seasons:"))
                return new EmbedModel() { content = content };

            var root = JsonSerializer.Deserialize<List<RootObject>>(Regex.Match(content, "seasons:([^\n\r]+)").Groups[1].Value);
            if (root == null || root.Count == 0)
                return null;

            return new EmbedModel() { serial = root };
        }
        #endregion

        #region Html
        public string Html(EmbedModel? md, string? imdb_id, long kinopoisk_id, string? title, string? original_title, int s)
        {
            if (md == null)
                return string.Empty;

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            if (md.content != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);

                string video = Regex.Match(md.content, dash ? "dash: +\"(https?://[^\"]+\\.mpd)\"" : "hls: +\"(https?://[^\"]+\\.m3u8)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(video))
                    return string.Empty;

                string name = Regex.Match(md.content, "audio: +\\{\"names\":\\[\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(name))
                    name = "По умолчанию";

                #region subtitle
                var subtitles = new SubtitleTpl();

                try
                {
                    var subs = JsonSerializer.Deserialize<List<Cc>>(Regex.Match(md.content, "cc: +(\\[[^\n\r]+\\]),").Groups[1].Value);
                    if (subs != null)
                    {
                        foreach (var cc in subs) 
                            subtitles.Append(cc.name, onstreamfile.Invoke(cc.url));
                    }
                }
                catch { }
                #endregion

                string voicename = Regex.Match(md.content, "audio: +\\{\"names\":\\[\"([^\\]]+)\\]").Groups[1].Value;
                voicename = voicename.Replace("\"", "").Replace("delete", "").Replace(",", ", ");
                voicename = Regex.Replace(voicename, "[, ]+$", "");

                return mtpl.ToHtml(name, onstreamfile.Invoke(video), subtitles: subtitles, voice_name: voicename);
                #endregion
            }
            else
            {
                #region Сериал
                string? enc_title = HttpUtility.UrlEncode(title);
                string? enc_original_title = HttpUtility.UrlEncode(original_title);

                try
                {
                    if (s == -1)
                    {
                        foreach (var season in md.serial.OrderBy(i => i.season))
                        {
                            string link = host + $"lite/collaps?kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&title={enc_title}&original_title={enc_original_title}&s={season.season}";

                            html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season.season} сезон" + "</div></div></div>");
                            firstjson = false;
                        }
                    }
                    else
                    {
                        var episodes = md.serial.First(i => i.season == s).episodes;
                        if (episodes == null)
                            return string.Empty;

                        foreach (var episode in episodes)
                        {
                            if (string.IsNullOrEmpty(dash ? episode?.dash : episode?.hls) || string.IsNullOrEmpty(episode?.episode))
                                continue;

                            #region voicename
                            string voicename = string.Empty;

                            if (episode?.audio?.names != null)
                                voicename = Regex.Replace(string.Join(", ", episode.audio.names), "[, ]+$", "");
                            #endregion

                            #region subtitle
                            var subtitles = new SubtitleTpl();

                            if (episode?.cc != null && episode.cc.Count > 0)
                            {
                                foreach (var cc in episode.cc)
                                    subtitles.Append(cc.name, onstreamfile.Invoke(cc.url));
                            }
                            #endregion

                            string file = onstreamfile.Invoke(dash ? episode.dash : episode.hls);
                            html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.episode + "\" data-json='{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + $"{title ?? original_title} ({episode.episode} серия)" + "\", \"subtitles\": [" + subtitles.ToHtml() + "], \"voice_name\":\"" + voicename + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.episode} серия" + "</div></div>");
                            firstjson = false;
                        }
                    }
                }
                catch
                {
                    return string.Empty;
                }
                #endregion
            }

            return html.ToString() + "</div>";
        }
        #endregion
    }
}

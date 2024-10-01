using Shared.Model.Online.VDBmovies;
using Shared.Model.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Shared.Engine.Online
{
    public class FanCDNInvoke
    {
        #region FanCDNInvoke
        string? host;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public FanCDNInvoke(string? host, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
        }
        #endregion

        #region Embed
        public List<Episode>? Embed(string? html)
        {
            if (string.IsNullOrEmpty(html))
                return null;

            string playlist = Regex.Match(html, "var playlist ?= ?(\\[[^\n\r]+\\]);").Groups[1].Value;
            if (string.IsNullOrEmpty(playlist))
                return null;

            var movies = JsonSerializer.Deserialize<List<Episode>>(playlist);
            if (movies == null || movies.Count == 0)
                return null;

            return movies;
        }
        #endregion

        #region Html
        public string Html(List<Episode>? movies, string? title, string? original_title)
        {
            if (movies == null)
                return string.Empty;

            var mtpl = new MovieTpl(title, original_title, movies.Count);

            foreach (var m in movies)
            {
                if (string.IsNullOrEmpty(m.file))
                    continue;

                #region subtitle
                var subtitles = new SubtitleTpl();

                if (!string.IsNullOrEmpty(m.subtitles))
                {
                    // [rus]rus1.srt,[eng]eng2.srt,[eng]eng3.srt
                    var match = new Regex("\\[([^\\]]+)\\]([^\\,]+)").Match(m.subtitles);
                    while (match.Success)
                    {
                        string srt = m.file.Replace("/hls.m3u8", "/") + match.Groups[2].Value;
                        subtitles.Append(match.Groups[1].Value, onstreamfile.Invoke(srt));
                        match = match.NextMatch();
                    }
                }
                #endregion

                mtpl.Append(m.title, onstreamfile.Invoke(m.file), subtitles: subtitles);
            }

            return mtpl.ToHtml();
        }
        #endregion
    }
}

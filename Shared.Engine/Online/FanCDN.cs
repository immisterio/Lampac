using Shared.Model.Online.VDBmovies;
using Shared.Model.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class FanCDNInvoke
    {
        #region FanCDNInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public FanCDNInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null; this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
        }
        #endregion

        #region Embed
        async public ValueTask<List<Episode>?> Embed(string title, string original_title, int year)
        {
            string? search = await onget($"{apihost}/index.php?do=search&subaction=search&search_start=0&full_search=1&result_from=1&story={HttpUtility.UrlEncode(original_title)}&titleonly=3&searchuser=&replyless=0&replylimit=0&searchdate=0&beforeafter=after&sortby=title&resorder=asc&showposts=0&catlist%5B%5D=10");
            if (string.IsNullOrEmpty(search))
                return null;

            string? href = null;

            foreach (string itemsearch in search.Split("item-search-serial"))
            {
                string? info = itemsearch.Split("torrent-link")?[0];
                if (string.IsNullOrEmpty(info) || !info.Contains($"({year}") || !info.Contains(title))
                    continue;

                href = Regex.Match(info, "<a href=\"(https?://[^\"]+\\.html)\"").Groups[1].Value;
                break;
            }

            if (string.IsNullOrEmpty(href))
                return null;

            string? html = await onget(href);
            if (string.IsNullOrEmpty(html))
                return null;

            string iframe_url = Regex.Match(html, "id=\"iframe-player\" src=\"([^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrEmpty(iframe_url))
                return null;

            string? iframe = await onget(iframe_url);
            if (string.IsNullOrEmpty(iframe))
                return null;

            iframe = Regex.Replace(iframe, "[\n\r\t]+", "").Replace("var ", "\n");

            string playlist = Regex.Match(iframe, "playlist ?= ?(\\[[^\n\r]+\\]);").Groups[1].Value;
            if (string.IsNullOrEmpty(playlist))
                return null;

            List<Episode>? movies = null;

            try
            {
                movies = JsonSerializer.Deserialize<List<Episode>>(playlist);
                if (movies == null || movies.Count == 0)
                    return null;
            }
            catch { return null; }

            return movies;
        }
        #endregion

        #region Html
        public string Html(List<Episode>? movies, string? title, string? original_title, bool rjson = false)
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

            return rjson ? mtpl.ToJson() : mtpl.ToHtml();
        }
        #endregion
    }
}

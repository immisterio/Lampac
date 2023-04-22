using Lampac.Models.LITE.Ashdi;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class AshdiInvoke
    {
        #region AshdiInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public AshdiInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
        }
        #endregion

        #region Embed
        public async ValueTask<string?> Embed(long kinopoisk_id)
        {
            if(kinopoisk_id == 0)
                return null;

            string? product = await onget.Invoke($"{apihost}/api/product/read_api.php?kinopoisk={kinopoisk_id}");
            if (product == null || !product.Contains("</iframe>"))
                return null;

            string iframeuri = Regex.Match(product, "src=\"(https?://[^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(iframeuri))
                return null;

            string? content = await onget.Invoke(iframeuri);
            if (content == null || !content.Contains("Playerjs"))
                return null;

            return content;
        }
        #endregion

        #region Html
        public string Html(string content, long kinopoisk_id, string? title, string? original_title, int t, int s)
        {
            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (!content.Contains("file:'[{"))
            {
                #region Фильм
                string hls = Regex.Match(content, "file:\"(https?://[^\"]+/index.m3u8)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(hls))
                    return string.Empty;

                #region subtitle
                string subtitles = string.Empty;

                string subtitle = new Regex("\"subtitle\":\"([^\"]+)\"").Match(content).Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(subtitle))
                {
                    var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(subtitle);
                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                        {
                            string suburl = onstreamfile.Invoke(match.Groups[2].Value);
                            subtitles += "{\"label\": \"" + match.Groups[1].Value + "\",\"url\": \"" + suburl + "\"},";
                        }

                        match = match.NextMatch();
                    }
                }

                subtitles = Regex.Replace(subtitles, ",$", "");
                #endregion

                hls = onstreamfile.Invoke(hls);
                html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + hls + "\",\"title\":\"" + (title ?? original_title) + "\", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">По умолчанию</div></div>";
                #endregion
            }
            else
            {
                #region Сериал
                try
                {
                    var root = JsonSerializer.Deserialize<List<Voice>>(new Regex("file:'([^\n\r]+)',").Match(content).Groups[1].Value);
                    if (root == null)
                        return string.Empty;

                    if (s == -1)
                    {
                        foreach (var voice in root)
                        {
                            foreach (var season in voice.folder)
                            {
                                if (html.Contains(season.title))
                                    continue;

                                string numberseason = Regex.Match(season.title, "([0-9]+)$").Groups[1].Value;
                                string link = host + $"lite/ashdi?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={numberseason}";

                                html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + season.title + "</div></div></div>";
                                firstjson = false;
                            }
                        }
                    }
                    else
                    {
                        #region Перевод
                        for (int i = 0; i < root.Count; i++)
                        {
                            if (root[i].folder.FirstOrDefault(i => i.title.EndsWith($" {s}")) == null)
                                continue;

                            if (t == -1)
                                t = i;

                            string link = host + $"lite/ashdi?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={s}&t={i}";

                            html += "<div class=\"videos__button selector " + (t == i ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + root[i].title + "</div>";
                        }

                        html += "</div><div class=\"videos__line\">";
                        #endregion

                        foreach (var episode in root[t].folder.First(i => i.title.EndsWith($" {s}")).folder)
                        {
                            #region subtitle
                            string subtitles = string.Empty;

                            if (!string.IsNullOrWhiteSpace(episode.subtitle))
                            {
                                var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(episode.subtitle);
                                while (match.Success)
                                {
                                    if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                                    {
                                        string suburl = onstreamfile.Invoke(match.Groups[2].Value);
                                        subtitles += "{\"label\": \"" + match.Groups[1].Value + "\",\"url\": \"" + suburl + "\"},";
                                    }

                                    match = match.NextMatch();
                                }
                            }

                            subtitles = Regex.Replace(subtitles, ",$", "");
                            #endregion

                            string file = onstreamfile.Invoke(episode.file);
                            html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(episode.title, "([0-9]+)$").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + $"{title ?? original_title} ({episode.title})" + "\", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + episode.title + "</div></div>";
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

            return html + "</div>";
        }
        #endregion
    }
}

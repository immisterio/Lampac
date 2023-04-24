using Lampac.Models.LITE.Ashdi;
using System.Text;
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
        public async ValueTask<EmbedModel?> Embed(long kinopoisk_id)
        {
            if(kinopoisk_id == 0)
                return null;

            string? product = await onget.Invoke($"{apihost}/api/product/read_api.php?kinopoisk={kinopoisk_id}");
            if (product == null)
                return null;

            string iframeuri = Regex.Match(product, "src=\"(https?://[^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(iframeuri))
                return null;

            string? content = await onget.Invoke(iframeuri);
            if (content == null || !content.Contains("Playerjs"))
                return null;

            if (!content.Contains("file:'[{"))
                return new EmbedModel() { content = content };

            var root = JsonSerializer.Deserialize<List<Voice>>(Regex.Match(content, "file:'([^\n\r]+)',").Groups[1].Value);
            if (root == null || root.Count == 0)
                return null;

            return new EmbedModel() { serial = root };
        }
        #endregion

        #region Html
        public string Html(EmbedModel md, long kinopoisk_id, string? title, string? original_title, int t, int s)
        {
            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            if (md.content != null)
            {
                #region Фильм
                string hls = Regex.Match(md.content, "file:\"(https?://[^\"]+/index.m3u8)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(hls))
                    return string.Empty;

                #region subtitle
                string subtitles = string.Empty;
                string subtitle = new Regex("\"subtitle\":\"([^\"]+)\"").Match(md.content).Groups[1].Value;

                if (!string.IsNullOrEmpty(subtitle))
                {
                    var subbuild = new StringBuilder();
                    var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(subtitle);
                    while (match.Success)
                    {
                        if (!string.IsNullOrEmpty(match.Groups[1].Value) && !string.IsNullOrEmpty(match.Groups[2].Value))
                        {
                            string suburl = onstreamfile.Invoke(match.Groups[2].Value);
                            subbuild.Append("{\"label\": \"" + match.Groups[1].Value + "\",\"url\": \"" + suburl + "\"},");
                        }

                        match = match.NextMatch();
                    }

                    if (subbuild.Length > 0)
                        subtitles = Regex.Replace(subbuild.ToString(), ",$", "");
                }
                #endregion

                hls = onstreamfile.Invoke(hls);
                html.Append("<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + hls + "\",\"title\":\"" + (title ?? original_title) + "\", \"subtitles\": [" + subtitle + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">По умолчанию</div></div>");
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
                        var hashseason = new HashSet<string>();

                        foreach (var voice in md.serial)
                        {
                            foreach (var season in voice.folder)
                            {
                                if (hashseason.Contains(season.title))
                                    continue;

                                hashseason.Add(season.title);
                                string numberseason = Regex.Match(season.title, "([0-9]+)$").Groups[1].Value;
                                if (string.IsNullOrEmpty(numberseason))
                                    continue;

                                string link = host + $"lite/ashdi?kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&s={numberseason}";

                                html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + season.title + "</div></div></div>");
                                firstjson = false;
                            }
                        }
                    }
                    else
                    {
                        #region Перевод
                        for (int i = 0; i < md.serial.Count; i++)
                        {
                            if (md.serial[i].folder.FirstOrDefault(i => i.title.EndsWith($" {s}")) == null)
                                continue;

                            if (t == -1)
                                t = i;

                            string link = host + $"lite/ashdi?kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&s={s}&t={i}";

                            html.Append("<div class=\"videos__button selector " + (t == i ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + md.serial[i].title + "</div>");
                        }

                        html.Append("</div><div class=\"videos__line\">");
                        #endregion

                        foreach (var episode in md.serial[t].folder.First(i => i.title.EndsWith($" {s}")).folder)
                        {
                            #region subtitle
                            string subtitles = string.Empty;

                            if (!string.IsNullOrEmpty(episode.subtitle))
                            {
                                var subbuild = new StringBuilder();
                                var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(episode.subtitle);
                                while (match.Success)
                                {
                                    if (!string.IsNullOrEmpty(match.Groups[1].Value) && !string.IsNullOrEmpty(match.Groups[2].Value))
                                    {
                                        string suburl = onstreamfile.Invoke(match.Groups[2].Value);
                                        subbuild.Append("{\"label\": \"" + match.Groups[1].Value + "\",\"url\": \"" + suburl + "\"},");
                                    }

                                    match = match.NextMatch();
                                }

                                if (subbuild.Length > 0)
                                    subtitles = Regex.Replace(subbuild.ToString(), ",$", "");
                            }
                            #endregion

                            string file = onstreamfile.Invoke(episode.file);
                            html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(episode.title, "([0-9]+)$").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + $"{title ?? original_title} ({episode.title})" + "\", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + episode.title + "</div></div>");
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

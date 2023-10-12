using Lampac.Models.LITE.Kinobase;
using Shared.Model.Online.Kinobase;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class KinobaseInvoke
    {
        #region KinobaseInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public KinobaseInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.onpost = onpost;
        }
        #endregion

        #region Embed
        async public ValueTask<EmbedModel?> Embed(string? title, int year, Func<string, ValueTask<string?>> oneval)
        {
            string? content = await onget($"{apihost}/search?query={HttpUtility.UrlEncode(title)}");
            if (content == null)
                return null;

            string? link = null, reservedlink = null;
            foreach (string row in content.Split("<div class=\"col-xs-2 item\">").Skip(1))
            {
                if (row.Contains(">Трейлер</span>"))
                    continue;

                var g = Regex.Match(row, "class=\"link\" alt=\"([^\"]+) \\(([0-9]{4})\\)\"").Groups;

                if (g[1].Value.ToLower().Trim() == title.ToLower())
                {
                    reservedlink = Regex.Match(row, "href=\"/([^\"]+)\"").Groups[1].Value;
                    if (string.IsNullOrEmpty(reservedlink))
                        continue;

                    if (g[2].Value == year.ToString())
                    {
                        link = reservedlink;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(link))
            {
                if (string.IsNullOrEmpty(reservedlink))
                    return null;

                link = reservedlink;
            }

            string? news = await onget($"{apihost}/{link}");
            if (news == null)
                return null;

            string MOVIE_ID = Regex.Match(news, "var MOVIE_ID = ([0-9]+)").Groups[1].Value;
            string IDENTIFIER = Regex.Match(news, "var IDENTIFIER = \"([^\"]+)").Groups[1].Value;
            string PLAYER_CUID = Regex.Match(news, "var PLAYER_CUID = \"([^\"]+)").Groups[1].Value;

            string? evalcode = await onget($"{apihost}/videoplayer.js?movie_id={MOVIE_ID}&IDENTIFIER={IDENTIFIER}&player_type=new&file_type=hls&_=1684592281");
            if (evalcode == null)
                return null;

            string? vod_url = await oneval(evalcode);
            if (string.IsNullOrEmpty(vod_url))
                return null;

            content = await onget(apihost + vod_url);
            if (content == null)
                return null;

            if (!content.Contains("file|"))
            {
                try
                {
                    var res = JsonSerializer.Deserialize<List<Season>>(Regex.Match(content, "^pl\\|(\\[[^\n\r]+\\])").Groups[1].Value);
                    if (res == null || res.Count == 0)
                        return null;

                    return new EmbedModel() { serial = res };
                }
                catch { return null; }
            }

            return new EmbedModel() { content = content };
        }
        #endregion

        #region Html
        public string Html(EmbedModel md, string? title, int year, int s)
        {
            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            #region getSubtitle
            string getSubtitle(string _sub)
            {
                if (string.IsNullOrWhiteSpace(_sub))
                    return string.Empty;

                var subtitles = new StringBuilder();
                var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,\\[\\| ]+\\.vtt)").Match(_sub);
                while (match.Success)
                {
                    if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                    {
                        string suburl = onstreamfile(match.Groups[2].Value);
                        subtitles.Append("{\"label\": \"" + match.Groups[1].Value + "\",\"url\": \"" + suburl + "\"},");
                    }

                    match = match.NextMatch();
                }

                if (subtitles.Length == 0)
                    return string.Empty;

                return Regex.Replace(subtitles.ToString(), ",$", "");
            }
            #endregion

            if (md.content != null)
            {
                #region Фильм
                string subtitles = getSubtitle(md.content);

                if (md.content.Contains("]{") && md.content.Contains(";"))
                {
                    foreach (var quality in new List<string> { "1080", "720", "480", "360" })
                    {
                        var g = new Regex($"\\[{quality}p?\\]([^\\[\\|\n\r,]+)").Match(md.content).Groups;
                        if (string.IsNullOrWhiteSpace(g[1].Value))
                            continue;

                        bool end = false;
                        var smatch = new Regex("\\{([^\\}]+)\\}(https?://[^\\[\\|;\n\r\t ]+.m3u8)").Match(g[1].Value);
                        while (smatch.Success)
                        {
                            if (!string.IsNullOrWhiteSpace(smatch.Groups[1].Value) && !string.IsNullOrWhiteSpace(smatch.Groups[2].Value))
                            {
                                string url = onstreamfile(smatch.Groups[2].Value);
                                html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + url + "\",\"title\":\"" + title + "\", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + smatch.Groups[1].Value + "</div></div>");
                                html.Append($"<!--{quality}p-->");
                                end = true;
                                firstjson = true;
                            }

                            smatch = smatch.NextMatch();
                        }

                        if (end)
                            break;
                    }
                }
                else
                {
                    foreach (Match m in Regex.Matches(md.content, $"\\[(1080|720|480|360)p?\\](\\{{[^\\}}]+\\}})?(https?://[^\\[\\|,;\n\r\t ]+.m3u8)").Reverse())
                    {
                        string link = m.Groups[3].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + onstreamfile(link) + "\",\"title\":\"" + title + "\", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + m.Groups[1].Value + "p</div></div>");
                        firstjson = true;
                    }
                }
                #endregion
            }
            else
            {
                #region getStreamLink
                (string hls, string streansquality) getStreamLink(string _data)
                {
                    var streams = new List<(string link, string quality)>() { Capacity = 3 };

                    foreach (Match m in Regex.Matches(_data, $"\\[(1080|720|480|360)p?\\](\\{{[^\\}}]+\\}})?(https?://[^\\[\\|,;\n\r\t ]+.m3u8)").Reverse())
                    {
                        string link = m.Groups[3].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        streams.Add((onstreamfile.Invoke(link), $"{m.Groups[1].Value}p"));
                    }

                    return (streams[0].link, "\"quality\": {" + string.Join(",", streams.Select(s => $"\"{s.quality}\":\"{s.link}\"")) + "}");
                }
                #endregion

                #region Сериал
                string? enc_title = HttpUtility.UrlEncode(title);

                if (s == -1)
                {
                    for (int i = 0; i < md.serial.Count; i++)
                    {
                        var season = md.serial[i];
                        if ((season?.playlist != null && season.playlist.Count > 0) || (season?.folder != null && season.folder.Count > 0))
                        {
                            string link = host + $"lite/kinobase?title={enc_title}&year={year}&s={i}";

                            html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + (season.comment ?? season.title) + "</div></div></div>");
                            firstjson = false;
                        }
                        else
                        {
                            if (season.file == null)
                                continue;

                            var streams = getStreamLink(season.file);
                            html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"1\" e=\"" + Regex.Match(season.comment ?? season.title, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams.hls + $"\",{streams.streansquality},\"title\":\"" + title + "\", \"subtitles\": [" + getSubtitle(season.subtitle) + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + (season.comment ?? season.title) + "</div></div>");
                            firstjson = false;
                        }
                    }
                }
                else
                {
                    string nameseason = Regex.Match(md.serial[s].comment ?? md.serial[s].title, "^([0-9]+)").Groups[1].Value;

                    var episodes = md.serial[s]?.playlist ?? md.serial[s]?.folder;
                    if (episodes == null)
                        return string.Empty;

                    foreach (var episode in episodes)
                    {
                        if (episode.file == null)
                            continue;

                        var streams = getStreamLink(episode.file);
                        html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + nameseason + "\" e=\"" + Regex.Match(episode.comment ?? episode.title, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams.hls + $"\",{streams.streansquality},\"title\":\"" + $"{title} ({episode.comment ?? episode.title})" + "\", \"subtitles\": [" + getSubtitle(episode.subtitle) + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + (episode.comment ?? episode.title) + "</div></div>");
                        firstjson = false;
                    }
                }
                #endregion
            }

            return html.ToString() + "</div>";
        }
        #endregion
    }
}

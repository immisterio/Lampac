using Lampac.Models.LITE;
using Shared.Model.Online.Rezka;
using Shared.Model.Templates;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class RezkaInvoke
    {
        #region RezkaInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public RezkaInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
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
        async public ValueTask<EmbedModel?> Embed(string? title, string? original_title, int clarification, int year, string? href)
        {
            var result = new EmbedModel();
            string? link = href, reservedlink = null;

            if (string.IsNullOrWhiteSpace(link))
            {
                string? search = await onget($"{apihost}/search/?do=search&subaction=search&q={HttpUtility.UrlEncode(clarification == 1 ? title : (original_title ?? title))}");
                if (search == null)
                    return null;

                foreach (string row in search.Split("\"b-content__inline_item\"").Skip(1))
                {
                    var g = Regex.Match(row, "href=\"(https?://[^\"]+)\">([^<]+)</a> ?<div>([0-9]{4})").Groups;

                    if (string.IsNullOrWhiteSpace(g[1].Value))
                        continue;

                    string name = g[2].Value.ToLower().Trim();
                    if (result.similar == null)
                        result.similar = new List<SimilarModel>();

                    result.similar.Add(new SimilarModel(name, g[3].Value, g[1].Value));

                    if (title != null && (name.Contains(" / ") && name.Contains(title.ToLower()) || name == title.ToLower()))
                    {
                        if (g[3].Value == year.ToString())
                        {
                            reservedlink = g[1].Value;
                            link = reservedlink;
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(link))
                {
                    if (string.IsNullOrWhiteSpace(reservedlink))
                        return result;

                    link = reservedlink;
                }
            }

            result.id = Regex.Match(link, "/([0-9]+)-[^/]+\\.html").Groups[1].Value;
            result.content = await onget(link);
            if (result.content == null || string.IsNullOrWhiteSpace(result.id))
                return null;

            return result;
        }
        #endregion

        #region Html
        public string Html(EmbedModel result, string? title, string? original_title, int clarification, int year, int s, string? href, bool showstream)
        {
            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);
            string? enc_href = HttpUtility.UrlEncode(href);

            #region similar html
            if (result.content == null)
            {
                if (string.IsNullOrWhiteSpace(href) && result.similar != null && result.similar.Count > 0)
                {
                    var stpl = new SimilarTpl(result.similar.Count);

                    foreach (var similar in result.similar)
                    {
                        string link = host + $"lite/rezka?title={enc_title}&original_title={enc_original_title}&clarification={clarification}&year={year}&href={HttpUtility.UrlEncode(similar.href)}";

                        stpl.Append(similar.title, similar.year, string.Empty, link);
                    }

                    return stpl.ToHtml();
                }

                return string.Empty;
            }
            #endregion

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            if (!result.content.Contains("data-season_id="))
            {
                #region Фильм
                var match = new Regex("<li [^>]+ data-translator_id=\"([0-9]+)\" ([^>]+)>([^<]+)").Match(result.content);
                if (match.Success)
                {
                    while (match.Success)
                    {
                        if (!string.IsNullOrEmpty(match.Groups[1].Value) && !string.IsNullOrEmpty(match.Groups[3].Value))
                        {
                            string favs = Regex.Match(result.content, "id=\"ctrl_favs\" value=\"([^\"]+)\"").Groups[1].Value;
                            string link = host + $"lite/rezka/movie?title={enc_title}&original_title={enc_original_title}&id={result.id}&t={match.Groups[1].Value}&favs={favs}";
                            string voice = match.Groups[3].Value.Trim();
                            if (voice == "-")
                                voice = "Оригинал";

                            if (match.Groups[2].Value.Contains("data-director=\"1\""))
                                link += "&director=1";

                            html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\",\"stream\":\"" + $"{link}&play=true" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + voice + "</div></div>");
                            firstjson = false;
                        }

                        match = match.NextMatch();
                    }
                }
                else
                {
                    var links = getStreamLink(Regex.Match(result.content, "\"id\":\"cdnplayer\",\"streams\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", ""));
                    if (links.Count == 0)
                        return string.Empty;

                    string streansquality = "\"quality\": {" + string.Join(",", links.Select(l => $"\"{l.title}\":\"{onstreamfile(l.stream_url!)}\"")) + "}";

                    html.Append("<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + onstreamfile(links[0].stream_url!) + "\",\"title\":\"" + title + "\"," + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + links[0].title + "</div></div>");
                }
                #endregion
            }
            else
            {
                #region Сериал
                string trs = new Regex("\\.initCDNSeriesEvents\\([0-9]+, ([0-9]+),").Match(result.content).Groups[1].Value;
                if (string.IsNullOrWhiteSpace(trs))
                    return string.Empty;

                #region Перевод
                if (result.content.Contains("data-translator_id="))
                {
                    var match = new Regex("data-translator_id=\"([0-9]+)\">([^<]+)(<img title=\"([^\"]+)\" [^>]+/>)?").Match(result.content);
                    while (match.Success)
                    {
                        string name = match.Groups[2].Value.Trim() + (string.IsNullOrWhiteSpace(match.Groups[4].Value) ? "" : $" ({match.Groups[4].Value})");
                        string link = host + $"lite/rezka/serial?title={enc_title}&original_title={enc_original_title}&clarification={clarification}&year={year}&href={enc_href}&id={result.id}&t={match.Groups[1].Value}";

                        html.Append("<div class=\"videos__button selector " + (match.Groups[1].Value == trs ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + name + "</div>");

                        match = match.NextMatch();
                    }

                    html.Append("</div><div class=\"videos__line\">");
                }
                #endregion

                HashSet<string> eshash = new HashSet<string>();
                var m = Regex.Match(result.content, "data-cdn_url=\"([^\"]+)\" [^>]+ data-season_id=\"([0-9]+)\" data-episode_id=\"([0-9]+)\">([^>]+)</li>");
                while (m.Success)
                {
                    if (s == -1)
                    {
                        #region Сезоны
                        string sname = $"{m.Groups[2].Value} сезон";
                        if (!string.IsNullOrEmpty(m.Groups[2].Value) && !eshash.Contains(sname))
                        {
                            eshash.Add(sname);
                            string link = host + $"lite/rezka?title={enc_title}&original_title={enc_original_title}&clarification={clarification}&year={year}&href={enc_href}&t={trs}&s={m.Groups[2].Value}";

                            html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + sname + "</div></div></div>");
                            firstjson = false;
                        }
                        #endregion
                    }
                    else
                    {
                        #region Серии
                        if (m.Groups[2].Value == s.ToString() && !eshash.Contains(m.Groups[4].Value))
                        {
                            eshash.Add(m.Groups[4].Value);
                            string link = host + $"lite/rezka/movie?title={enc_title}&original_title={enc_original_title}&id={result.id}&t={trs}&s={s}&e={m.Groups[3].Value}";

                            string streamlink = string.Empty;
                            if (showstream)
                                streamlink = ",\"stream\":\"" + $"{link}&play=true" + "\"";

                            html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + m.Groups[3].Value + "\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\"" + streamlink + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + m.Groups[4].Value + "</div></div>");
                            firstjson = false;
                        }
                        #endregion
                    }

                    m = m.NextMatch();
                }
                #endregion
            }

            return html.ToString() + "</div>";
        }
        #endregion


        #region Serial
        async public ValueTask<Episodes?> SerialEmbed(long id, int t)
        {
            string uri = $"{apihost}/ajax/get_cdn_series/?t={((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds()}";
            string data = $"id={id}&translator_id={t}&action=get_episodes";

            Episodes? root = null;

            try
            {
                root = JsonSerializer.Deserialize<Episodes>(await onpost(uri, data));
            }
            catch { }

            if (root == null)
                return null;

            string? episodes = root.episodes;
            if (string.IsNullOrWhiteSpace(episodes) || episodes.ToLower() == "false")
                return null;

            return root;
        }

        public string Serial(Episodes root, EmbedModel result, string? title, string? original_title, int clarification, int year, string? href, long id, int t, int s, bool showstream)
        {
            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);
            string? enc_href = HttpUtility.UrlEncode(href);

            #region Перевод
            {
                if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
                    return string.Empty;

                if (result?.content != null)
                {
                    if (result.content.Contains("data-translator_id="))
                    {
                        var match = new Regex("data-translator_id=\"([0-9]+)\">([^<]+)(<img title=\"([^\"]+)\" [^>]+/>)?").Match(result.content);
                        while (match.Success)
                        {
                            string name = match.Groups[2].Value.Trim() + (string.IsNullOrWhiteSpace(match.Groups[4].Value) ? "" : $" ({match.Groups[4].Value})");
                            string link = host + $"lite/rezka/serial?title={enc_title}&original_title={enc_original_title}&clarification={clarification}&year={year}&href={enc_href}&id={id}&t={match.Groups[1].Value}";

                            html += "<div class=\"videos__button selector " + (match.Groups[1].Value == t.ToString() ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + name + "</div>";

                            match = match.NextMatch();
                        }

                        html += "</div><div class=\"videos__line\">";
                    }
                }
            }
            #endregion

            if (s == -1)
            {
                #region Сезоны
                var match = new Regex("data-tab_id=\"([0-9]+)\">([^<]+)</li>").Match(root.seasons);
                while (match.Success)
                {
                    string link = host + $"lite/rezka/serial?title={enc_title}&original_title={enc_original_title}&clarification={clarification}&year={year}&href={enc_href}&id={id}&t={t}&s={match.Groups[1].Value}";

                    html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{match.Groups[1].Value} сезон" + "</div></div></div>";
                    firstjson = false;

                    match = match.NextMatch();
                }
                #endregion
            }
            else
            {
                #region Серии
                var m = new Regex($"data-season_id=\"{s}\" data-episode_id=\"([0-9]+)\">([^<]+)</li>").Match(root.episodes);
                while (m.Success)
                {
                    if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[2].Value))
                    {
                        string link = host + $"lite/rezka/movie?title={enc_title}&original_title={enc_original_title}&id={id}&t={t}&s={s}&e={m.Groups[1].Value}";

                        string streamlink = string.Empty;
                        if (showstream)
                            streamlink = ",\"stream\":\"" + $"{link}&play=true" + "\"";

                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + m.Groups[1].Value + "\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\"" + streamlink + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + m.Groups[2].Value + "</div></div>";
                        firstjson = false;
                    }

                    m = m.NextMatch();
                }
                #endregion
            }

            return html + "</div>";
        }
        #endregion

        #region Movie
        async public ValueTask<MovieModel?> Movie(long id, int t, int director, int s, int e, string? favs)
        {
            string? data = null;
            string uri = $"{apihost}/ajax/get_cdn_series/?t={((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds()}";

            if (s == -1)
            {
                data = $"id={id}&translator_id={t}&is_camrip=0&is_ads=0&is_director={director}&favs={favs}&action=get_movie";
            }
            else
            {
                data = $"id={id}&translator_id={t}&season={s}&episode={e}&favs={favs}&action=get_stream";
            }
            string? json = await onpost(uri, data);
            if (string.IsNullOrEmpty(json))
                return null;

            Dictionary<string, object>? root = null;

            try
            {
                root = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            }
            catch { return null; }

            string? url = root?["url"]?.ToString();
            if (string.IsNullOrWhiteSpace(url) || url.ToLower() == "false")
                return null;

            var links = getStreamLink(url);
            if (links.Count == 0)
                return null;

            string? subtitlehtml = null;

            try
            {
                subtitlehtml = root?["subtitle"]?.ToString();
            }
            catch { }

            return new MovieModel() { links = links, subtitlehtml = subtitlehtml };
        }

        public string? Movie(MovieModel md, string? title, string? original_title, bool play)
        {
            if (play)
                return onstreamfile(md.links[0].stream_url!);

            #region subtitles
            string subtitles = string.Empty;

            try
            {
                if (!string.IsNullOrWhiteSpace(md.subtitlehtml))
                {
                    var m = Regex.Match(md.subtitlehtml, "\\[([^\\]]+)\\](https?://[^\n\r,']+\\.vtt)");
                    while (m.Success)
                    {
                        if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[2].Value))
                            subtitles += "{\"label\": \"" + m.Groups[1].Value + "\",\"url\": \"" + onstreamfile(m.Groups[2].Value) + "\"},";

                        m = m.NextMatch();
                    }
                }
            }
            catch { }
            #endregion

            string streansquality = "\"quality\": {" + string.Join(",", md.links.Select(s => $"\"{s.title}\":\"{onstreamfile(s.stream_url!)}\"")) + "}";

            return "{\"method\":\"play\",\"url\":\"" + onstreamfile(md.links[0].stream_url!) + "\",\"title\":\"" + (title ?? original_title) + "\",\"subtitles\": [" + Regex.Replace(subtitles, ",$", "") + "]," + streansquality + "}";
        }
        #endregion


        #region decodeBase64
        static string decodeBase64(string _data)
        {
            if (_data.Contains("#"))
            {
                string[] trashList = new string[] { "QEA=", "QCM=", "QCE=", "QF4=", "QCQ=", "I0A=", "IyM=", "IyE=", "I14=", "IyQ=", "IUA=", "ISM=", "ISE=", "IV4=", "ISQ=", "XkA=", "XiM=", "XiE=", "Xl4=", "XiQ=", "JEA=", "JCM=", "JCE=", "JF4=", "JCQ=", "QEBA", "QEAj", "QEAh", "QEBe", "QEAk", "QCNA", "QCMj", "QCMh", "QCNe", "QCMk", "QCFA", "QCEj", "QCEh", "QCFe", "QCEk", "QF5A", "QF4j", "QF4h", "QF5e", "QF4k", "QCRA", "QCQj", "QCQh", "QCRe", "QCQk", "I0BA", "I0Aj", "I0Ah", "I0Be", "I0Ak", "IyNA", "IyMj", "IyMh", "IyNe", "IyMk", "IyFA", "IyEj", "IyEh", "IyFe", "IyEk", "I15A", "I14j", "I14h", "I15e", "I14k", "IyRA", "IyQj", "IyQh", "IyRe", "IyQk", "IUBA", "IUAj", "IUAh", "IUBe", "IUAk", "ISNA", "ISMj", "ISMh", "ISNe", "ISMk", "ISFA", "ISEj", "ISEh", "ISFe", "ISEk", "IV5A", "IV4j", "IV4h", "IV5e", "IV4k", "ISRA", "ISQj", "ISQh", "ISRe", "ISQk", "XkBA", "XkAj", "XkAh", "XkBe", "XkAk", "XiNA", "XiMj", "XiMh", "XiNe", "XiMk", "XiFA", "XiEj", "XiEh", "XiFe", "XiEk", "Xl5A", "Xl4j", "Xl4h", "Xl5e", "Xl4k", "XiRA", "XiQj", "XiQh", "XiRe", "XiQk", "JEBA", "JEAj", "JEAh", "JEBe", "JEAk", "JCNA", "JCMj", "JCMh", "JCNe", "JCMk", "JCFA", "JCEj", "JCEh", "JCFe", "JCEk", "JF5A", "JF4j", "JF4h", "JF5e", "JF4k", "JCRA", "JCQj", "JCQh", "JCRe", "JCQk" };

                _data = _data.Remove(0, 2).Replace("//_//", "");

                foreach (string trash in trashList)
                    _data = _data.Replace(trash, "");

                _data = Regex.Replace(_data, "//[^/]+_//", "").Replace("//_//", "");
                _data = Encoding.UTF8.GetString(Convert.FromBase64String(_data));


                //_data = Regex.Replace(_data, "/[^/=]+=([^\n\r\t= ])", "/$1");
                //_data = _data.Replace("//_//", "");
                //_data = Regex.Replace(_data, "//[^/]+_//", "");
                //_data = _data.Replace("QEBAQEAhIyMhXl5e", "");
                //_data = _data.Replace("IyMjI14hISMjIUBA", "");
                //_data = Regex.Replace(_data, "CQhIUAkJEBeIUAjJCRA.", "");
                //_data = _data.Replace("QCMhQEBAIyMkJXl5eXl5eIyNAEBA", "");

                //// Lampa
                //_data = _data.Replace("QCMhQEBAIyMkJEBA", "");
                //_data = _data.Replace("Xl5eXl5eIyNAzN2FkZmRm", "");

                //_data = Encoding.UTF8.GetString(Convert.FromBase64String(_data.Remove(0, 2)));
            }

            return _data;
        }
        #endregion

        #region getStreamLink
        List<ApiModel> getStreamLink(string _data)
        {
            _data = decodeBase64(_data);
            var links = new List<ApiModel>() { Capacity = 6 };

            #region getLink
            string? getLink(string _q)
            {
                string link = new Regex($"\\[{_q}\\][^ ]+ or (https?://[^\n\r ]+.mp4)(,|$)").Match(_data).Groups[1].Value;

                if (string.IsNullOrWhiteSpace(link))
                    link = new Regex($"\\[{_q}\\](https?://[^\n\r, ]+.mp4([^\n\r, ]+)?)").Match(_data).Groups[1].Value;

                if (string.IsNullOrWhiteSpace(link) || !Regex.IsMatch(link, "^[a-z0-9/\\-:\\.\\=]+$", RegexOptions.IgnoreCase))
                    return null;

                return link;
            }
            #endregion

            #region Максимально доступное
            foreach (var q in new List<string> { "2160p", "1440p", "1080p Ultra", "1080p", "720p", "480p", "360p" })
            {
                string? link = getLink(q);
                if (string.IsNullOrEmpty(link))
                    continue;

                if (!link.Contains(".m3u"))
                    link += ":hls:manifest.m3u8";

                links.Add(new ApiModel()
                {
                    title = q.Contains("p") ? q : $"{q}p",
                    stream_url = link
                });
            }
            #endregion

            return links;
        }
        #endregion
    }
}

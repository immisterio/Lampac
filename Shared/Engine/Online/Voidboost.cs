using Lampac.Engine.CORE;
using Lampac.Models.LITE;
using Shared.Model.Online.Rezka;
using Shared.Model.Templates;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class VoidboostInvoke
    {
        #region VoidboostInvoke
        string? host;
        string apihost;
        bool usehls;
        Func<string, ValueTask<string?>> onget;
        Func<string, string, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;
        Action? requesterror;

        public VoidboostInvoke(string? host, string apihost, bool hls, Func<string, ValueTask<string?>> onget, Func<string, string, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null, Action? requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            usehls = hls;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.onpost = onpost;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        async public ValueTask<string?> Embed(string? imdb_id, long kinopoisk_id, string? t)
        {
            string uri = $"{apihost}/embed/";
            if (kinopoisk_id > 0 && !string.IsNullOrWhiteSpace(imdb_id))
                uri += $"{imdb_id},{kinopoisk_id}";
            else
                uri += (kinopoisk_id > 0 ? kinopoisk_id.ToString() : imdb_id);

            if (!string.IsNullOrWhiteSpace(t))
                uri = $"{apihost}/serial/{t}/iframe";

            string? content = await onget(uri);
            if (string.IsNullOrEmpty(content))
            {
                requesterror?.Invoke();
                return null;
            }

            return content;
        }
        #endregion

        #region Html
        public string Html(string? content, string? imdb_id, long kinopoisk_id, string? title, string? original_title, string? t)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            if (!content.Contains("id=\"season-number\""))
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);

                foreach (Match m in Regex.Matches(content, "<option data-token=\"([^\"]+)\" [^>]+>([^<]+)</option>"))
                {
                    if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[2].Value))
                    {
                        string link = host + $"lite/voidboost/movie?title={enc_title}&original_title={enc_original_title}&t={m.Groups[1].Value}";
                        string voice = m.Groups[2].Value.Trim();
                        if (voice == "-" || string.IsNullOrEmpty(voice))
                            voice = "Оригинал";

                        mtpl.Append(voice, link, "call", $"{link}&play=true");
                    }
                }

                return mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Перевод
                string? activTranslate = t;

                foreach (Match m in Regex.Matches(content, "<option data-token=\"([^\"]+)\" [^>]+>([^<]+)</option>"))
                {
                    if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[2].Value))
                    {
                        if (string.IsNullOrEmpty(activTranslate))
                            activTranslate = m.Groups[1].Value;

                        string link = host + $"lite/voidboost?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&t={m.Groups[1].Value}";

                        string active = string.IsNullOrWhiteSpace(t) ? (firstjson ? "active" : "") : (t == m.Groups[1].Value ? "active" : "");

                        string voice = m.Groups[2].Value.Trim();
                        if (voice != "-")
                        {
                            html.Append("<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + voice + "</div>");
                            firstjson = false;
                        }
                    }
                }

                html.Append("</div>");
                #endregion

                #region Сезоны
                firstjson = true;
                html.Append("<div class=\"videos__line\">");

                string? seasonrow = StringConvert.FindLastText(content, "name=\"season\"", "</select>");
                if (seasonrow == null)
                    return string.Empty;

                foreach (Match m in Regex.Matches(seasonrow, "<option value=\"([0-9]+)\"([^>]+)?>([^<]+)</option>"))
                {
                    if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[3].Value))
                    {
                        string link = host + $"lite/voidboost/serial?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&t={activTranslate}&s={m.Groups[1].Value}";

                        html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + m.Groups[3].Value + "</div></div></div>");
                        firstjson = false;
                    }
                }
                #endregion
            }

            return html.ToString() + "</div>";
        }
        #endregion


        #region Serial
        async public ValueTask<string?> Serial(string? imdb_id, long kinopoisk_id, string? title, string? original_title, string? t, int s, bool showstream)
        {
            string? content = await onget($"{apihost}/serial/{t}/iframe?s={s}");
            if (content == null || !content.Contains("name=\"episode\""))
            {
                requesterror?.Invoke();
                return null;
            }

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            #region Перевод
            string? activTranslate = t;

            foreach (Match m in Regex.Matches(content, "<option data-token=\"([^\"]+)\" [^>]+>([^<]+)</option>"))
            {
                if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[2].Value))
                {
                    if (string.IsNullOrEmpty(activTranslate))
                        activTranslate = m.Groups[1].Value;

                    string link = host + $"lite/voidboost?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&t={m.Groups[1].Value}";

                    string active = string.IsNullOrWhiteSpace(t) ? (firstjson ? "active" : "") : (t == m.Groups[1].Value ? "active" : "");

                    string voice = m.Groups[2].Value.Trim();
                    if (voice != "-")
                    {
                        html.Append("<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + voice + "</div>");
                        firstjson = false;
                    }
                }
            }

            html.Append("</div>");
            #endregion

            #region Серии
            firstjson = true;
            html.Append("<div class=\"videos__line\">");

            string? seasonrow = StringConvert.FindLastText(content, "name=\"episode\"", "</select>");
            if (seasonrow == null)
                return string.Empty;

            foreach (Match m in Regex.Matches(seasonrow, "<option value=\"([^\"]+)\"([^>]+)?>([^<]+)</option>"))
            {
                if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[3].Value))
                {
                    string link = host + $"lite/voidboost/movie?title={enc_title}&original_title={enc_original_title}&t={t}&s={s}&e={m.Groups[1].Value}";

                    string stream = showstream ? $",\"stream\":\"{link}&play=true\"" : string.Empty;
                    html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + m.Groups[1].Value + "\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\"" + stream + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + m.Groups[3].Value + "</div></div>");
                    firstjson = false;
                }
            }
            #endregion

            return html.ToString() + "</div>";
        }
        #endregion

        #region Movie
        async public ValueTask<MovieModel?> Movie(string? t, int s, int e)
        {
            string uri = $"{apihost}/movie/{t}/iframe";
            if (s > 0 || e > 0)
                uri = $"{apihost}/serial/{t}/iframe?s={s}&e={e}";

            string? content = await onget(uri);
            if (content == null)
            {
                requesterror?.Invoke();
                return null;
            }

            var mfile = Regex.Match(content, "'file': ?'([^']+)'");
            if (!mfile.Success)
                return null;

            return new MovieModel() { url = mfile.Groups[1].Value.Trim(), subtitlehtml = Regex.Match(content, "'subtitle': '([^']+)'").Groups[1].Value };
        }

        public string Movie(MovieModel? md, string? title, string? original_title, bool play)
        {
            if (md == null)
                return string.Empty;

            #region subtitle
            string subtitles = string.Empty;

            if (!string.IsNullOrWhiteSpace(md.subtitlehtml))
            {
                var m = Regex.Match(md.subtitlehtml, "\\[([^\\]]+)\\](https?://[^\n\r,']+\\.vtt)");
                while (m.Success)
                {
                    if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[2].Value))
                        subtitles += "{\"label\": \"" + m.Groups[1].Value + "\",\"url\": \"" + (onstreamfile(m.Groups[2].Value)) + "\"},";

                    m = m.NextMatch();
                }

                subtitles = Regex.Replace(subtitles, ",$", "");
            }
            #endregion

            var links = getStreamLink(md.url);

            string streansquality = string.Empty;
            foreach (var l in links)
                streansquality += $"\"{l.title}\":\"" + l.stream_url + "\",";

            if (play)
                return links[0].stream_url!;

            return "{\"method\":\"play\",\"url\":\"" + links[0].stream_url + "\",\"title\":\"" + (title ?? original_title) + "\", \"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}, \"subtitles\": [" + subtitles + "]}";
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
            var links = new List<ApiModel>() { Capacity = 4 };

            onlog?.Invoke(_data);

            #region getLink
            string? getLink(string _q)
            {
                string qline = Regex.Match(_data, $"\\[{_q}\\]([^\\[]+)").Groups[1].Value;
                if (!qline.Contains(".mp4") && !qline.Contains(".m3u8"))
                    return null;

                string link = usehls ? Regex.Match(qline, "(https?://[^\\[\n\r, ]+:manifest.m3u8)").Groups[1].Value : string.Empty;

                if (string.IsNullOrEmpty(link))
                    link = Regex.Match(qline, "(https?://[^\\[\n\r, ]+\\.mp4)(,| |$)").Groups[1].Value;

                if (string.IsNullOrEmpty(link))
                {
                    link = Regex.Match(qline, "(https?://[^\\[\n\r, ]+)").Groups[1].Value;
                    if (string.IsNullOrEmpty(link))
                        return null;
                }

                return link;
            }
            #endregion

            #region Максимально доступное
            foreach (string q in new List<string> { "1080p", "720p", "480p", "360p" })
            {
                string? link = getLink(q);
                if (string.IsNullOrEmpty(link))
                    continue;

                links.Add(new ApiModel()
                {
                    title = q,
                    stream_url = onstreamfile(link)
                });
            }
            #endregion

            return links;
        }
        #endregion
    }
}

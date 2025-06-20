using Lampac.Engine.CORE;
using Shared.Model.Base;
using Shared.Model.Online.Eneyida;
using Shared.Model.Templates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class KinoukrInvoke
    {
        #region unic
        static string ArrayList => "qwertyuioplkjhgfdsazxcvbnm";
        static string ArrayListToNumber => "1234567890";
        public static string unic(int size = 8, bool IsNumberCode = false)
        {
            StringBuilder array = new StringBuilder();
            for (int i = 0; i < size; i++)
            {
                array.Append(IsNumberCode ? ArrayListToNumber[Random.Shared.Next(0, ArrayListToNumber.Length)] : ArrayList[Random.Shared.Next(0, ArrayList.Length)]);
            }

            return array.ToString();
        }
        #endregion

        #region KinoukrInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;
        Action? requesterror;

        public KinoukrInvoke(in string? host, in string apihost, Func<string, ValueTask<string?>> onget, Func<string, string, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null, Action? requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onpost = onpost;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        public async ValueTask<EmbedModel?> Embed(string? original_title, int year, string? href)
        {
            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(original_title) || year == 0))
                return null;

            string? link = href;
            var result = new EmbedModel();

            if (string.IsNullOrWhiteSpace(link))
            {
                EmbedModel? kurwa = await EmbedKurwa(original_title, year);
                if (kurwa != null)
                    return kurwa;

                return kurwa;

                onlog?.Invoke("search start");
                //string? search = await onget.Invoke($"{apihost}/index.php?do=search&subaction=search&from_page=0&story={HttpUtility.UrlEncode(original_title)}");

                // $"{apihost}/index.php?do=search"
                string? search = await onpost.Invoke($"{apihost}/{unic(4, true)}-{unic(Random.Shared.Next(4, 8))}-{unic(Random.Shared.Next(5, 10))}.html", $"do=search&subaction=search&story={HttpUtility.UrlEncode(original_title)}");
                if (search == null)
                {
                    requesterror?.Invoke();
                    return null;
                }

                onlog?.Invoke("search ok");

                foreach (string row in search.Split("\"short clearfix with-mask\"").Skip(1))
                {
                    if (row.Contains(">Анонс</div>") || row.Contains(">Трейлер</div>"))
                        continue;

                    string newslink = Regex.Match(row, "href=\"(https?://[^/]+/[^\"]+\\.html)\"").Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(newslink))
                        continue;

                    string name = Regex.Match(row, "class=\"short-title\" [^>]+>([^<]+)<").Groups[1].Value;
                    if (result.similars == null)
                        result.similars = new List<Similar>();

                    result.similars.Add(new Similar() 
                    {
                        title = name,
                        href = newslink
                    });
                }

                if (result.similars == null || result.similars.Count == 0)
                {
                    if (search.Contains(">Пошук по сайту<"))
                        return new EmbedModel() { IsEmpty = true };

                    return null;
                }

                if (result.similars.Count > 1)
                    return result;

                link = result.similars[0].href;
            }

            onlog?.Invoke("link: " + link);
            string? news = await onget.Invoke(link);
            if (news == null)
            {
                requesterror?.Invoke();
                return null;
            }

            result.quel = Regex.Match(news, "class=\"m-meta m-qual\">([^<]+)<").Groups[1].Value;

            string iframeUri = Regex.Match(news, "src=\"(https?://tortuga\\.[a-z]+/[^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrEmpty(iframeUri))
            {
                iframeUri = Regex.Match(news, "src=\"(https?://ashdi\\.vip/[^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(iframeUri))
                    return null;
            }

            onlog?.Invoke("iframeUri: " + iframeUri);
            string? content = await onget.Invoke(iframeUri);
            if (content == null || !content.Contains("file:"))
            {
                requesterror?.Invoke();
                return null;
            }

            string? player = StringConvert.FindLastText(content, "new Playerjs", "</script>");
            if (player == null)
                return null;

            if (Regex.IsMatch(content, "file: ?'\\["))
            {
                List<Lampac.Models.LITE.Ashdi.Voice>? root = null;

                try
                {
                    root = JsonSerializer.Deserialize<List<Lampac.Models.LITE.Ashdi.Voice>>(Regex.Match(content, "file: ?'([^\n\r]+)',").Groups[1].Value);
                    if (root == null || root.Count == 0)
                        return null;
                }
                catch { return null; }

                result.serial = root;
            }
            else
            {
                result.content = player;
                onlog?.Invoke("content: " + result.content);
            }

            return result;
        }
        #endregion

        #region EmbedKurwa
        public async ValueTask<EmbedModel?> EmbedKurwa(string? original_title, int year)
        {
            if (string.IsNullOrWhiteSpace(original_title) || year == 0)
                return null;

            var result = new EmbedModel();

            string? json = await onget.Invoke($"https://bobr-kurwa.men/ukr?eng_name={HttpUtility.UrlEncode(original_title)}");
            if (json == null)
            {
                requesterror?.Invoke();
                return null;
            }

            BobrKurwa? kurwa = null;

            try
            {
                foreach (var item in JsonSerializer.Deserialize<List<BobrKurwa>>(json))
                {
                    if (item.year == year.ToString())
                    {
                        kurwa = item;
                        break;
                    }
                }
            }
            catch { }

            if (kurwa == null)
                return new EmbedModel() {  IsEmpty = true };

            string iframeUri = kurwa.tortuga ?? kurwa.ashdi;

            onlog?.Invoke("iframeUri: " + iframeUri);
            string? content = await onget.Invoke(iframeUri);
            if (content == null || !content.Contains("file:"))
            {
                requesterror?.Invoke();
                return null;
            }

            string? player = StringConvert.FindLastText(content, "new TortugaCore", "</script>");
            if (player == null)
                return null;

            if (Regex.IsMatch(content, "file: ?'\\["))
            {
                List<Lampac.Models.LITE.Ashdi.Voice>? root = null;

                try
                {
                    root = JsonSerializer.Deserialize<List<Lampac.Models.LITE.Ashdi.Voice>>(Regex.Match(content, "file: ?'([^\n\r]+)',").Groups[1].Value);
                    if (root == null || root.Count == 0)
                        return null;
                }
                catch { return null; }

                result.serial = root;
            }
            else
            {
                result.content = player;
                onlog?.Invoke("content: " + result.content);
            }

            return result;
        }
        #endregion

        #region Html
        public string Html(EmbedModel? result, in int clarification, in string? title, in string? original_title, in int year, int t, int s, in string? href, VastConf? vast = null, in bool rjson = false)
        {
            if (result == null || result.IsEmpty)
                return string.Empty;

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            #region similar
            if (result.content == null && result.serial == null)
            {
                if (string.IsNullOrWhiteSpace(href) && result.similars != null && result.similars.Count > 0)
                {
                    var stpl = new SimilarTpl(result.similars.Count);

                    foreach (var similar in result.similars)
                    {
                        string link = host + $"lite/kinoukr?rjson={rjson}&clarification={clarification}&title={enc_title}&original_title={enc_original_title}&year={year}&href={HttpUtility.UrlEncode(similar.href)}";

                        stpl.Append(similar.title, similar.year, string.Empty, link);
                    }

                    return rjson ? stpl.ToJson() : stpl.ToHtml();
                }

                return string.Empty;
            }
            #endregion

            string fixStream(in string _l) => _l.Replace("0yql3tj", "oyql3tj");

            if (result.content != null)
            {
                #region Фильм
                onlog?.Invoke("movie");

                var mtpl = new MovieTpl(title, original_title, 1);

                string hls = Regex.Match(result.content, "file: ?\"(https?://[^\"]+/index.m3u8)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(hls))
                    return string.Empty;

                #region subtitle
                var subtitles = new SubtitleTpl();
                string subtitle = new Regex("\"subtitle\": ?\"([^\"]+)\"").Match(result.content).Groups[1].Value;

                if (!string.IsNullOrEmpty(subtitle))
                {
                    var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(subtitle);
                    while (match.Success)
                    {
                        subtitles.Append(match.Groups[1].Value, onstreamfile.Invoke(fixStream(match.Groups[2].Value)));
                        match = match.NextMatch();
                    }
                }
                #endregion

                mtpl.Append(string.IsNullOrEmpty(result.quel) ? "По умолчанию" : result.quel, onstreamfile.Invoke(fixStream(hls)), subtitles: subtitles, vast: vast);

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                string? enc_href = HttpUtility.UrlEncode(href);

                try
                {
                    if (s == -1)
                    {
                        #region Сезоны
                        var tpl = new SeasonTpl();
                        var hashseason = new HashSet<string>();

                        foreach (var voice in result.serial)
                        {
                            foreach (var season in voice.folder)
                            {
                                if (hashseason.Contains(season.title))
                                    continue;

                                hashseason.Add(season.title);
                                string numberseason = Regex.Match(season.title, "([0-9]+)$").Groups[1].Value;
                                if (string.IsNullOrEmpty(numberseason)) 
                                    continue;

                                string link = host + $"lite/kinoukr?rjson={rjson}&clarification={clarification}&title={enc_title}&original_title={enc_original_title}&year={year}&href={enc_href}&s={numberseason}";

                                tpl.Append(season.title, link, numberseason);
                            }
                        }

                        return rjson ? tpl.ToJson() : tpl.ToHtml();
                        #endregion
                    }
                    else
                    {
                        #region Перевод
                        var vtpl = new VoiceTpl();

                        for (int i = 0; i < result.serial.Count; i++)
                        {
                            if (result.serial[i].folder.FirstOrDefault(i => i.title.EndsWith($" {s}")) == null)
                                continue;

                            if (t == -1)
                                t = i;

                            string link = host + $"lite/kinoukr?rjson={rjson}&clarification={clarification}&title={enc_title}&original_title={enc_original_title}&year={year}&href={enc_href}&s={s}&t={i}";
                            vtpl.Append(result.serial[i].title, t == i, link);
                        }
                        #endregion

                        var etpl = new EpisodeTpl();
                        string sArhc = s.ToString();

                        foreach (var episode in result.serial[t].folder.First(i => i.title.EndsWith($" {s}")).folder)
                        {
                            #region subtitle
                            var subtitles = new SubtitleTpl();

                            if (!string.IsNullOrEmpty(episode.subtitle))
                            {
                                var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(episode.subtitle);
                                while (match.Success)
                                {
                                    subtitles.Append(match.Groups[1].Value, onstreamfile.Invoke(fixStream(match.Groups[2].Value)));
                                    match = match.NextMatch();
                                }
                            }
                            #endregion

                            string file = onstreamfile.Invoke(fixStream(episode.file));
                            etpl.Append(episode.title, title ?? original_title, sArhc, Regex.Match(episode.title, "([0-9]+)$").Groups[1].Value, file, subtitles: subtitles, vast: vast);
                        }

                        if (rjson)
                            return etpl.ToJson(vtpl);

                        return vtpl.ToHtml() + etpl.ToHtml();
                    }
                }
                catch
                {
                    return string.Empty;
                }
                #endregion
            }
        }
        #endregion
    }
}

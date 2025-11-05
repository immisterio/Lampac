using Shared.Models.Base;
using Shared.Models.Online.Eneyida;
using Shared.Models.Templates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public struct KinoukrInvoke
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
        string host;
        string apihost;
        Func<string, ValueTask<string>> onget;
        Func<string, string, ValueTask<string>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string> onlog;
        Action requesterror;

        public KinoukrInvoke(string host, string apihost, Func<string, ValueTask<string>> onget, Func<string, string, ValueTask<string>> onpost, Func<string, string> onstreamfile, Func<string, string> onlog = null, Action requesterror = null)
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
        public async ValueTask<EmbedModel> Embed(string original_title, int year, string href)
        {
            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(original_title) || year == 0))
                return null;

            return null;

            string link = href;
            var result = new EmbedModel();

            if (string.IsNullOrWhiteSpace(link))
            {
                onlog?.Invoke("search start");
                //string? search = await onget.Invoke($"{apihost}/index.php?do=search&subaction=search&from_page=0&story={HttpUtility.UrlEncode(original_title)}");

                // $"{apihost}/index.php?do=search"
                string search = await onpost.Invoke($"{apihost}/{unic(4, true)}-{unic(Random.Shared.Next(4, 8))}-{unic(Random.Shared.Next(5, 10))}.html", $"do=search&subaction=search&story={HttpUtility.UrlEncode(original_title)}");
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
            string news = await onget.Invoke(link);
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
            string content = await onget.Invoke(iframeUri);
            if (content == null || !content.Contains("file:"))
            {
                requesterror?.Invoke();
                return null;
            }

            string player = StringConvert.FindLastText(content, "new Playerjs", "</script>");
            if (player == null)
                return null;

            if (Regex.IsMatch(content, "file: ?'\\["))
            {
                Models.Online.Tortuga.Voice[] root = null;

                try
                {
                    root = JsonSerializer.Deserialize<Models.Online.Tortuga.Voice[]>(Regex.Match(content, "file: ?'([^\n\r]+)',").Groups[1].Value);
                    if (root == null || root.Length == 0)
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
        public async ValueTask<EmbedModel> EmbedKurwa(int clarification, string title, string original_title, int year, string href)
        {
            string iframeUri = href;
            var result = new EmbedModel();

            if (string.IsNullOrEmpty(iframeUri))
            {
                string arg = clarification == 1 || string.IsNullOrWhiteSpace(original_title) 
                    ? $"name={HttpUtility.UrlEncode(title)}" 
                    : $"eng_name={HttpUtility.UrlEncode(original_title)}";

                string json = await onget.Invoke("http://194.246.82.144/ukr?" + arg);
                if (json == null)
                {
                    requesterror?.Invoke();
                    return null;
                }

                result.similars = new List<Similar>();

                try
                {
                    foreach (var item in JsonSerializer.Deserialize<BobrKurwa[]>(json))
                    {
                        var model = new Similar()
                        {
                            href = item.tortuga ?? item.ashdi,
                            title = $"{item.name} / {item.eng_name}",
                            year = item.year
                        };

                        if (item.year == year.ToString())
                            result.similars.Insert(0, model);
                        else
                            result.similars.Add(model);
                    }
                }
                catch { }

                if (result.similars.Count == 0)
                    return new EmbedModel() { IsEmpty = true };

                if (result.similars.Count > 1 && result.similars[0].year != year.ToString())
                    return result;

                iframeUri = result.similars[0].href;
            }

            onlog?.Invoke("iframeUri: " + iframeUri);
            string content = await onget.Invoke(iframeUri);
            if (content == null || !content.Contains("file:"))
            {
                requesterror?.Invoke();
                return null;
            }

            if (iframeUri.Contains("ashdi"))
            {
                result.source_type = "ashdi";

                if (Regex.IsMatch(content, "file: ?'\\["))
                {
                    Models.Online.Ashdi.Voice[] root = null;

                    try
                    {
                        root = JsonSerializer.Deserialize<Models.Online.Ashdi.Voice[]>(Regex.Match(content, "file: ?'([^\n\r]+)',").Groups[1].Value);
                        if (root == null || root.Length == 0)
                            return null;
                    }
                    catch { return null; }

                    result.serial_ashdi = root;
                }
                else
                {
                    result.content = content;
                    onlog?.Invoke("content: " + result.content);
                }

                return result;
            }
            else
            {
                result.source_type = "tortuga";

                if (Regex.IsMatch(content, "file: ?'"))
                {
                    Models.Online.Tortuga.Voice[] root = null;

                    try
                    {
                        string file = Regex.Match(content, "file: ?'([^\n\r]+)',").Groups[1].Value;
                        if (file.EndsWith("=="))
                        {
                            file = Regex.Replace(file, "==$", "");
                            file = string.Join("", CrypTo.DecodeBase64(file).Reverse());
                        }

                        root = JsonSerializer.Deserialize<Models.Online.Tortuga.Voice[]>(file);
                        if (root == null || root.Length == 0)
                            return null;
                    }
                    catch { return null; }

                    result.serial = root;
                }
                else
                {
                    string player = StringConvert.FindLastText(content, "new TortugaCore", "</script>");

                    result.content = player ?? content;
                    onlog?.Invoke("content: " + result.content);
                }
            }

            return result;
        }
        #endregion

        #region getIframeSource
        public async ValueTask<string> getIframeSource(string link)
        {
            if (string.IsNullOrWhiteSpace(link))
                return null;

            string news = await onget.Invoke(link);
            if (news == null)
            {
                requesterror?.Invoke();
                return null;
            }

            string iframeUri = Regex.Match(news, "src=\"(https?://tortuga\\.[a-z]+/[^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrEmpty(iframeUri))
            {
                iframeUri = Regex.Match(news, "src=\"(https?://ashdi\\.vip/[^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(iframeUri))
                    return null;
            }

            return iframeUri;
        }
        #endregion

        #region Html
        public string Html(EmbedModel result, int clarification, string title, string original_title, int year, string t, int s, string href, VastConf vast = null, bool rjson = false)
        {
            if (result == null || result.IsEmpty)
                return string.Empty;

            string enc_title = HttpUtility.UrlEncode(title);
            string enc_original_title = HttpUtility.UrlEncode(original_title);
            string enc_href = HttpUtility.UrlEncode(href);

            #region similar
            if (result.content == null && result.serial == null && result.serial_ashdi == null)
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

            if (result.source_type == "ashdi")
            {
                var invk = new AshdiInvoke(host, apihost, onget, onstreamfile, onlog: onlog, requesterror: requesterror);
                int.TryParse(t, out int _t);

                var md = new Models.Online.Ashdi.EmbedModel()
                {
                    content = result.content,
                    serial = result.serial_ashdi
                };

                return invk.Html(md, 0, title, original_title, _t, s, vast, rjson, host + $"lite/kinoukr?rjson={rjson}&clarification={clarification}&title={enc_title}&original_title={enc_original_title}&year={year}&href={enc_href}");
            }

            if (result.content != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, 1);

                string hls = Regex.Match(result.content, "file: ?(\"|')(?<hls>https?://[^\"']+/index\\.m3u8)(\"|')").Groups["hls"].Value;
                if (string.IsNullOrWhiteSpace(hls))
                {
                    string base64 = Regex.Match(result.content, "file: ?(\"|')(?<base64>[^\"']+)(\"|')").Groups["base64"].Value;
                           base64 = Regex.Replace(base64, "==$", "");

                    hls = string.Join("", CrypTo.DecodeBase64(base64).Reverse());

                    if (string.IsNullOrWhiteSpace(hls))
                        return string.Empty;
                }

                #region subtitle
                var subtitles = new SubtitleTpl();
                string subtitle = new Regex("\"subtitle\": ?\"([^\"]+)\"").Match(result.content).Groups[1].Value;

                if (!string.IsNullOrEmpty(subtitle))
                {
                    var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(subtitle);
                    while (match.Success)
                    {
                        subtitles.Append(match.Groups[1].Value, onstreamfile.Invoke(match.Groups[2].Value));
                        match = match.NextMatch();
                    }
                }
                #endregion

                mtpl.Append(string.IsNullOrEmpty(result.quel) ? "По умолчанию" : result.quel, onstreamfile.Invoke(hls), subtitles: subtitles, vast: vast);

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                try
                {
                    if (s == -1)
                    {
                        #region Сезоны
                        var tpl = new SeasonTpl();

                        foreach (var season in result.serial)
                        {
                            string link = host + $"lite/kinoukr?rjson={rjson}&clarification={clarification}&title={enc_title}&original_title={enc_original_title}&year={year}&href={enc_href}&s={season.season}";

                            tpl.Append(season.title, link, season.season);
                        }

                        return rjson ? tpl.ToJson() : tpl.ToHtml();
                        #endregion
                    }
                    else
                    {
                        #region Перевод
                        var vtpl = new VoiceTpl();
                        var hashVoice = new HashSet<string>();

                        foreach (var season in result.serial)
                        {
                            foreach (var episode in season.folder)
                            {
                                foreach (var voice in episode.folder)
                                {
                                    if (hashVoice.Contains(voice.title))
                                        continue;
                                    hashVoice.Add(voice.title);

                                    if (string.IsNullOrEmpty(t))
                                        t = voice.title;

                                    string link = host + $"lite/kinoukr?rjson={rjson}&clarification={clarification}&title={enc_title}&original_title={enc_original_title}&year={year}&href={enc_href}&s={s}&t={voice.title}";
                                    vtpl.Append(voice.title, t == voice.title, link);
                                }
                            }
                        }
                        #endregion

                        string sArhc = s.ToString();
                        var episodes = result.serial.First(i => i.season == sArhc).folder;
                        var etpl = new EpisodeTpl(episodes.Length);

                        foreach (var episode in episodes)
                        {
                            var video = episode.folder.FirstOrDefault(i => i.title == t);
                            if (video.file == null)
                                continue;

                            #region subtitle
                            var subtitles = new SubtitleTpl();

                            if (!string.IsNullOrEmpty(video.subtitle))
                            {
                                var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(video.subtitle);
                                while (match.Success)
                                {
                                    subtitles.Append(match.Groups[1].Value, onstreamfile.Invoke(match.Groups[2].Value));
                                    match = match.NextMatch();
                                }
                            }
                            #endregion

                            string file = onstreamfile.Invoke(video.file);
                            etpl.Append(episode.title, title ?? original_title, sArhc, episode.number, file, subtitles: subtitles, vast: vast);
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

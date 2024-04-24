using Shared.Model.Online;
using Shared.Model.Online.Kodik;
using Shared.Model.Templates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class KodikInvoke
    {
        #region KodikInvoke
        static Dictionary<string, string> psingles = new Dictionary<string, string>();

        string? host;
        string apihost, token;
        bool usehls;
        Func<string, List<HeadersModel>?, ValueTask<string?>> onget;
        Func<string, string, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;
        Action? requesterror;

        public KodikInvoke(string? host, string apihost, string token, bool hls, Func<string, List<HeadersModel>?, ValueTask<string?>> onget, Func<string, string, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null, Action? requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.token = token;
            this.onget = onget;
            this.onpost = onpost;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.usehls = hls;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        public async ValueTask<List<Result>?> Embed(string? imdb_id, long kinopoisk_id, int s)
        {
            string url = $"{apihost}/search?token={token}&limit=100&with_episodes=true";
            if (kinopoisk_id > 0)
                url += $"&kinopoisk_id={kinopoisk_id}";

            if (!string.IsNullOrWhiteSpace(imdb_id))
                url += $"&imdb_id={imdb_id}";

            if (s > 0)
                url += $"&season={s}";

            try
            {
                string? json = await onget(url, null);
                if (json == null)
                {
                    requesterror?.Invoke();
                    return null;
                }

                var root = JsonSerializer.Deserialize<RootObject>(json);
                if (root?.results == null)
                    return null;

                return root.results;
            }
            catch { return null; }
        }


        public async ValueTask<EmbedModel?> Embed(string title)
        {
            try
            {
                string url = $"{apihost}/search?token={token}&limit=100&title={HttpUtility.UrlEncode(title)}&with_episodes=true";

                string? json = await onget(url, null);
                if (json == null)
                {
                    requesterror?.Invoke();
                    return null;
                }

                var root = JsonSerializer.Deserialize<RootObject>(json);
                if (root?.results == null)
                    return null;

                var hash = new HashSet<string>();
                var stpl = new SimilarTpl(root.results.Count);
                string enc_title = HttpUtility.UrlEncode(title);

                foreach (var similar in root.results)
                {
                    string? pick = similar.title?.ToLower()?.Trim();
                    if (string.IsNullOrEmpty(pick))
                        continue;

                    if (hash.Contains(pick))
                        continue;

                    hash.Add(pick);

                    string? name = !string.IsNullOrEmpty(similar.title) && !string.IsNullOrEmpty(similar.title_orig) ? $"{similar.title} / {similar.title_orig}" : (similar.title ?? similar.title_orig);
                    
                    string details = similar.translation.title;
                    if (similar.last_season > 0)
                        details += $"{stpl.OnlineSplit} {similar.last_season}й сезон";

                    stpl.Append(name, similar.year.ToString(), details, host + $"lite/kodik?title={enc_title}&clarification=1&pick={HttpUtility.UrlEncode(pick)}");
                }

                return new EmbedModel()
                {
                    html = stpl.ToHtml(),
                    result = root.results
                };
            }
            catch { return null; }
        }

        public List<Result> Embed(List<Result> results, string pick)
        {
            var content = new List<Result>();

            foreach (var i in results)
            {
                if (i.title == null || i.title.ToLower().Trim() != pick)
                    continue;

                content.Add(i);
            }

            return content;
        }
        #endregion

        #region Html
        public string Html(List<Result> results, string? imdb_id, long kinopoisk_id, string? title, string? original_title, int clarification, string? pick, string? kid, int s, bool showstream)
        {
            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            if (results[0].type is "foreign-movie" or "soviet-cartoon" or "foreign-cartoon" or "russian-cartoon" or "anime" or "russian-movie")
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, results.Count);

                foreach (var data in results)
                {
                    string url = host + $"lite/kodik/video?title={enc_title}&original_title={enc_original_title}&link={HttpUtility.UrlEncode(data.link)}";

                    string streamlink = string.Empty;
                    if (showstream)
                        streamlink = $"{url.Replace("/video", "/video.m3u8")}&play=true";

                    mtpl.Append(data.translation.title, url, "call", streamlink);
                }

                return mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                string? enc_pick = HttpUtility.UrlEncode(pick);

                if (s == -1)
                {
                    var hash = new HashSet<int>();

                    foreach (var item in results.AsEnumerable().Reverse())
                    {
                        int season = item.last_season;
                        string link = host + $"lite/kodik?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&clarification={clarification}&pick={enc_pick}&s={season}";

                        if (hash.Contains(season))
                            continue;

                        hash.Add(season);
                        html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season} сезон" + "</div></div></div>");
                        firstjson = false;
                    }
                }
                else
                {
                    #region Перевод
                    HashSet<string> hash = new HashSet<string>();

                    foreach (var item in results)
                    {
                        string id = item.id;
                        if (string.IsNullOrWhiteSpace(id))
                            continue;

                        string name = item.translation?.title ?? "оригинал";
                        if (hash.Contains(name) || !results.First(i => i.id == id).seasons.ContainsKey(s.ToString()))
                            continue;

                        hash.Add(name);

                        if (string.IsNullOrWhiteSpace(kid))
                            kid = id;

                        string link = host + $"lite/kodik?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&clarification={clarification}&pick={enc_pick}&s={s}&kid={id}";

                        html.Append("<div class=\"videos__button selector " + (kid == id ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + name + "</div>");
                    }

                    html.Append("</div><div class=\"videos__line\">");
                    #endregion

                    foreach (var episode in results.First(i => i.id == kid).seasons[s.ToString()].episodes)
                    {
                        string url = host + $"lite/kodik/video?title={enc_title}&original_title={enc_original_title}&link={HttpUtility.UrlEncode(episode.Value)}&episode={episode.Key}";

                        string streamlink = string.Empty;
                        if (showstream)
                            streamlink = "\"stream\":\"" + $"{url.Replace("/video", "/video.m3u8")}&play=true" + "\",";

                        html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.Key + "\" data-json='{\"method\":\"call\",\"url\":\"" + url + "\"," + streamlink + "\"title\":\"" + $"{title ?? original_title} ({episode.Key} серия)" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.Key} серия" + "</div></div>");
                        firstjson = false;
                    }
                }
                #endregion
            }

            return html.ToString() + "</div>";
        }
        #endregion

        #region VideoParse
        async public ValueTask<List<StreamModel>?> VideoParse(string linkhost, string link)
        {
            string? iframe = await onget($"https:{link}", null);
            if (iframe == null)
            {
                requesterror?.Invoke();
                return null;
            }

            string? uri = null;
            string player_single = Regex.Match(iframe, "src=\"/(assets/js/app\\.player_[^\"]+\\.js)\"").Groups[1].Value;
            if (!string.IsNullOrEmpty(player_single))
            {
                if (!psingles.TryGetValue(player_single, out uri))
                {
                    string? playerjs = await onget($"{linkhost}/{player_single}", null);

                    if (playerjs == null)
                    {
                        requesterror?.Invoke();
                        return null;
                    }

                    uri = DecodeUrlBase64(Regex.Match(playerjs, "type:\"POST\",url:atob\\(\"([^\"]+)\"\\)").Groups[1].Value);
                    if (!string.IsNullOrEmpty(uri))
                        psingles.TryAdd(player_single, uri);
                }
            }

            if (string.IsNullOrEmpty(uri))
                return null;

            string _frame = Regex.Replace(iframe.Split("advertDebug")[1].Split("preview-icons")[0], "[\n\r\t ]+", "");
            string domain = Regex.Match(_frame, "domain=\"([^\"]+)\"").Groups[1].Value;
            string d_sign = Regex.Match(_frame, "d_sign=\"([^\"]+)\"").Groups[1].Value;
            string pd = Regex.Match(_frame, "pd=\"([^\"]+)\"").Groups[1].Value;
            string pd_sign = Regex.Match(_frame, "pd_sign=\"([^\"]+)\"").Groups[1].Value;
            string ref_domain = Regex.Match(_frame, "ref=\"([^\"]+)\"").Groups[1].Value;
            string ref_sign = Regex.Match(_frame, "ref_sign=\"([^\"]+)\"").Groups[1].Value;
            string type = Regex.Match(_frame, "videoInfo.type='([^']+)'").Groups[1].Value;
            string hash = Regex.Match(_frame, "videoInfo.hash='([^']+)'").Groups[1].Value;
            string id = Regex.Match(_frame, "videoInfo.id='([^']+)'").Groups[1].Value;

            string? json = await onpost($"{linkhost + uri}", $"d={domain}&d_sign={d_sign}&pd={pd}&pd_sign={pd_sign}&ref={ref_domain}&ref_sign={ref_sign}&type={type}&hash={hash}&id={id}&info=%7B%7D");
            if (json == null || !json.Contains("\"src\":\""))
            {
                requesterror?.Invoke();
                return null;
            }

            var streams = new List<StreamModel>();

            var match = new Regex("\"([0-9]+)p?\":\\[\\{\"src\":\"([^\"]+)", RegexOptions.IgnoreCase).Match(json);
            while (match.Success)
            {
                if (!string.IsNullOrWhiteSpace(match.Groups[2].Value))
                {
                    int zCharCode = Convert.ToInt32('Z');

                    string src = Regex.Replace(match.Groups[2].Value, "[a-zA-Z]", e => {
                        int eCharCode = Convert.ToInt32(e.Value[0]);
                        return ((eCharCode <= zCharCode ? 90 : 122) >= (eCharCode = eCharCode + 13) ? (char)eCharCode : (char)(eCharCode - 26)).ToString();
                    });

                    string decodedString = DecodeUrlBase64(src);

                    if (decodedString.StartsWith("//"))
                        decodedString = $"https:{decodedString}";

                    if (!usehls && decodedString.Contains(".m3u"))
                        decodedString = decodedString.Replace(":hls:manifest.m3u8", "");

                    streams.Insert(0, new StreamModel() { q = $"{match.Groups[1].Value}p", url = decodedString });
                }

                match = match.NextMatch();
            }

            if (streams.Count == 0)
                return null;

            return streams;
        }

        public string VideoParse(List<StreamModel>? streams, string? title, string? original_title, int episode, bool play)
        {
            if (streams == null || streams.Count == 0)
                return string.Empty;

            if (play)
                return onstreamfile(streams[0].url);

            string streansquality = "\"quality\": {" + string.Join(",", streams.Select(s => $"\"{s.q}\":\"{onstreamfile(s.url)}\"")) + "}";

            string name = title ?? original_title;
            if (episode > 0)
                name += $" ({episode} серия)";

            return "{\"method\":\"play\",\"url\":\"" + onstreamfile(streams[0].url) + "\",\"title\":\"" + name + "\", " + streansquality + "}";
        }
        #endregion


        #region DecodeUrlBase64
        static string DecodeUrlBase64(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/').PadRight(4 * ((s.Length + 3) / 4), '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(s));
        }
        #endregion
    }
}

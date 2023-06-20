using Shared.Model.Online.Kodik;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class KodikInvoke
    {
        #region KodikInvoke
        string? host;
        string apihost, token;
        Func<string, List<(string name, string val)>?, ValueTask<string?>> onget;
        Func<string, string, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public KodikInvoke(string? host, string apihost, string token, Func<string, List<(string name, string val)>?, ValueTask<string?>> onget, Func<string, string, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.token = token;
            this.onget = onget;
            this.onpost= onpost;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
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
                var root = JsonSerializer.Deserialize<RootObject>(await onget(url, null));
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

                var root = JsonSerializer.Deserialize<RootObject>(await onget(url, null));
                if (root?.results == null)
                    return null;

                bool firstjson = true;
                var html = new StringBuilder();
                html.Append("<div class=\"videos__line\">");

                var hash = new HashSet<string>();
                string enc_title = HttpUtility.UrlEncode(title);

                foreach (var similar in root.results)
                {
                    string pick = similar.title.ToLower().Trim();
                    if (hash.Contains(pick))
                        continue;

                    hash.Add(pick);
                    string link = host + $"lite/kodik?title={enc_title}&clarification=1&pick={HttpUtility.UrlEncode(pick)}";

                    html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\",\"similar\":true}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + similar.title + "</div></div></div>");
                    firstjson = false;
                }

                return new EmbedModel()
                {
                    html = html + "</div>",
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
                foreach (var data in results)
                {
                    string link = data.link;
                    string voice = data.translation.title;

                    string url = host + $"lite/kodik/video?title={enc_title}&original_title={enc_original_title}&link={HttpUtility.UrlEncode(link)}";

                    string streamlink = string.Empty;
                    if (showstream)
                        streamlink = "\"stream\":\"" + $"{url.Replace("/video", "/video.m3u8")}&play=true" + "\",";

                    html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"call\",\"url\":\"" + url + "\", " + streamlink + "\"title\":\"" + $"{title ?? original_title} ({voice})" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + voice + "</div></div>");
                    firstjson = false;
                }
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
        async public ValueTask<string?> VideoParse(string linkhost, string? title, string? original_title, string link, int episode, bool play)
        {
            string? iframe = await onget($"http:{link}", new List<(string name, string val)>() { ("referer", "https://animego.org/") });
            if (iframe == null)
                return null;

            string _frame = Regex.Replace(iframe, "[\n\r\t ]+", "");
            string d_sign = new Regex("d_sign=\"([^\"]+)\"").Match(_frame).Groups[1].Value;
            string pd_sign = new Regex("pd_sign=\"([^\"]+)\"").Match(_frame).Groups[1].Value;
            string ref_sign = new Regex("ref_sign=\"([^\"]+)\"").Match(_frame).Groups[1].Value;
            string type = new Regex("videoInfo.type='([^']+)'").Match(_frame).Groups[1].Value;
            string hash = new Regex("videoInfo.hash='([^']+)'").Match(_frame).Groups[1].Value;
            string id = new Regex("videoInfo.id='([^']+)'").Match(_frame).Groups[1].Value;

            string? json = await onpost($"{linkhost}/gvi", $"d=animego.org&d_sign={d_sign}&pd=kodik.info&pd_sign={pd_sign}&ref=https%3A%2F%2Fanimego.org%2F&ref_sign={ref_sign}&bad_user=false&type={type}&hash={hash}&id={id}&info=%7B%22advImps%22%3A%7B%7D%7D");
            if (json == null)
                return null;

            var streams = new List<(string q, string url)>();

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
                        decodedString = $"http:{decodedString}";

                    streams.Insert(0, ($"{match.Groups[1].Value}p", decodedString));
                }

                match = match.NextMatch();
            }

            if (streams.Count == 0)
                return null;


            string streansquality = string.Empty;
            foreach (var l in streams)
            {
                string hls = onstreamfile(l.url);
                streansquality += $"\"{l.q}\":\"" + hls + "\",";
            }

            string name = title ?? original_title;
            if (episode > 0)
                name += $" ({episode} серия)";

            if (play)
                return streams[0].url;

            return "{\"method\":\"play\",\"url\":\"" + streams[0].url + "\",\"title\":\"" + name + "\", \"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}}";
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

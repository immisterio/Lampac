using Shared.Model.Base;
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
        string apihost, token, videopath;
        bool usehls, cdn_is_working;
        Func<string, List<HeadersModel>?, ValueTask<string?>> onget;
        Func<string, string, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;
        Action? requesterror;

        public KodikInvoke(string? host, string apihost, string token, bool hls, bool cdn_is_working, string videopath, Func<string, List<HeadersModel>?, ValueTask<string?>> onget, Func<string, string, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null, Action? requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.token = token;
            this.videopath = videopath;
            this.onget = onget;
            this.onpost = onpost;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.usehls = hls;
            this.cdn_is_working = cdn_is_working;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        async public ValueTask<List<Result>?> Embed(string? imdb_id, long kinopoisk_id, int s)
        {
            if (string.IsNullOrEmpty(imdb_id) && kinopoisk_id == 0)
                return null;

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


        public async ValueTask<EmbedModel?> Embed(string title, string original_title, int clarification)
        {
            try
            {
                if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(original_title))
                    return null;

                string url = $"{apihost}/search?token={token}&limit=100&title={HttpUtility.UrlEncode(original_title ?? title)}&with_episodes=true&with_material_data=true";

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
                string? enc_original_title = HttpUtility.UrlEncode(original_title);

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

                    var matd = similar.material_data;
                    string? img = PosterApi.Size(matd?.anime_poster_url ?? matd?.drama_poster_url ?? matd?.poster_url);
                    stpl.Append(name, similar.year.ToString(), details, host + $"lite/kodik?title={enc_title}&original_title={enc_original_title}&clarification={clarification}&pick={HttpUtility.UrlEncode(pick)}", img);
                }

                return new EmbedModel()
                {
                    stpl = stpl,
                    result = root.results
                };
            }
            catch { return null; }
        }

        public List<Result> Embed(List<Result> results, in string pick)
        {
            var content = new List<Result>(results.Count);

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
        public string Html(List<Result> results, string args, in string? imdb_id, in long kinopoisk_id, in string? title, in string? original_title, in int clarification, in string? pick, string? kid, in int s, in bool showstream, in bool rjson)
        {
            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            if (results[0].type is "foreign-movie" or "soviet-cartoon" or "foreign-cartoon" or "russian-cartoon" or "anime" or "russian-movie")
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, results.Count);

                foreach (var data in results)
                {
                    string url = host + $"lite/kodik/video?title={enc_title}&original_title={enc_original_title}&link={HttpUtility.UrlEncode(data.link)}";

                    string? streamlink = null;
                    if (showstream)
                    {
                        streamlink = usehls ? $"{url.Replace("/video", $"/{videopath}.m3u8")}&play=true" : $"{url.Replace("/video", $"/{videopath}")}&play=true";

                        if (!string.IsNullOrEmpty(args))
                            streamlink += $"&{args.Remove(0, 1)}";
                    }

                    mtpl.Append(data.translation.title, url, "call", streamlink);
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                string? enc_pick = HttpUtility.UrlEncode(pick);

                if (s == -1)
                {
                    var tpl = new SeasonTpl(results.Count);
                    var hash = new HashSet<int>();

                    foreach (var item in results.AsEnumerable().Reverse())
                    {
                        int season = item.last_season;
                        string link = host + $"lite/kodik?rjson={rjson}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&clarification={clarification}&pick={enc_pick}&s={season}";

                        if (hash.Contains(season))
                            continue;

                        hash.Add(season);
                        tpl.Append($"{season} сезон", link, season);
                    }

                    return rjson ? tpl.ToJson() : tpl.ToHtml();
                }
                else
                {
                    #region Перевод
                    var vtpl = new VoiceTpl();
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

                        string link = host + $"lite/kodik?rjson={rjson}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&clarification={clarification}&pick={enc_pick}&s={s}&kid={id}";

                        vtpl.Append(name, kid == id, link);
                    }
                    #endregion

                    var series = results.First(i => i.id == kid).seasons[s.ToString()].episodes;
                    var etpl = new EpisodeTpl(series.Count);

                    string sArhc = s.ToString();

                    foreach (var episode in series)
                    {
                        string url = host + $"lite/kodik/video?title={enc_title}&original_title={enc_original_title}&link={HttpUtility.UrlEncode(episode.Value)}&episode={episode.Key}";

                        string? streamlink = null;
                        if (showstream)
                        {
                            streamlink = usehls ? $"{url.Replace("/video", $"/{videopath}.m3u8")}&play=true" : $"{url.Replace("/video", $"/{videopath}")}&play=true";

                            if (!string.IsNullOrEmpty(args))
                                streamlink += $"&{args.Remove(0, 1)}";
                        }

                        etpl.Append($"{episode.Key} серия", title ?? original_title, sArhc, episode.Key, url, "call", streamlink: streamlink);
                    }

                    if (rjson)
                        return etpl.ToJson(vtpl);

                    return vtpl.ToHtml() + etpl.ToHtml();
                }
                #endregion
            }
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

            string? json = await onpost($"{linkhost + uri}", $"d={domain}&d_sign={d_sign}&pd={pd}&pd_sign={pd_sign}&ref={ref_domain}&ref_sign={ref_sign}&bad_user=false&cdn_is_working={cdn_is_working.ToString().ToLower()}&type={type}&hash={hash}&id={id}&info=%7B%7D");
            if (json == null || !json.Contains("\"src\":\""))
            {
                requesterror?.Invoke();
                return null;
            }

            var streams = new List<StreamModel>(4);

            var match = new Regex("\"([0-9]+)p?\":\\[\\{\"src\":\"([^\"]+)", RegexOptions.IgnoreCase).Match(json);
            while (match.Success)
            {
                if (!string.IsNullOrWhiteSpace(match.Groups[2].Value))
                {
                    string m3u = match.Groups[2].Value;
                    if (!m3u.Contains("manifest.m3u8"))
                    {
                        int zCharCode = Convert.ToInt32('Z');

                        string src = Regex.Replace(match.Groups[2].Value, "[a-zA-Z]", e =>
                        {
                            int eCharCode = Convert.ToInt32(e.Value[0]);
                            return ((eCharCode <= zCharCode ? 90 : 122) >= (eCharCode = eCharCode + 18) ? (char)eCharCode : (char)(eCharCode - 26)).ToString();
                        });

                        m3u = DecodeUrlBase64(src);
                    }

                    if (m3u.StartsWith("//"))
                        m3u = $"https:{m3u}";

                    if (!usehls && m3u.Contains(".m3u"))
                        m3u = m3u.Replace(":hls:manifest.m3u8", "");

                    streams.Add(new StreamModel() { q = $"{match.Groups[1].Value}p", url = m3u });
                }

                match = match.NextMatch();
            }

            if (streams.Count == 0)
                return null;

            streams.Reverse();

            return streams;
        }

        public string VideoParse(List<StreamModel>? streams, in string? title, in string? original_title, in int episode, in bool play, VastConf? vast = null)
        {
            if (streams == null || streams.Count == 0)
                return string.Empty;

            if (play)
                return onstreamfile(streams[0].url);

            string name = title ?? original_title ?? "auto";
            if (episode > 0)
                name += $" ({episode} серия)";

            var streamquality = new StreamQualityTpl();
            foreach (var l in streams)
                streamquality.Append(onstreamfile(l.url), l.q);

            return VideoTpl.ToJson("play", onstreamfile(streams[0].url), name, streamquality: streamquality, vast: vast);
        }
        #endregion


        #region DecodeUrlBase64
        static string DecodeUrlBase64(in string s)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/').PadRight(4 * ((s.Length + 3) / 4), '=')));
        }
        #endregion
    }
}

using Org.BouncyCastle.Utilities.IO;
using Shared.Models.Online.Settings;
using Shared.Models.Online.VideoCDN;
using Shared.Models.Templates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public struct VideoCDNInvoke
    {
        #region VideoCDNInvoke
        string host, scheme;
        string iframeapihost;
        string apihost;
        string token;
        bool usehls;
        Func<string, string, ValueTask<string>> onget;
        Func<string, string> onstreamfile;
        Func<string, string> onlog;
        Action requesterror;

        public string onstream(string stream)
        {
            if (onstreamfile == null)
                return stream;

            return onstreamfile.Invoke(stream);
        }

        public VideoCDNInvoke(OnlinesSettings init, Func<string, string, ValueTask<string>> onget, Func<string, string> onstreamfile, string host = null, Func<string, string> onlog = null, Action requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.scheme = init.scheme;
            this.iframeapihost = init.corsHost();
            this.apihost = init.cors(init.apihost);
            this.token = init!.token;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            usehls = init.hls;
            this.requesterror = requesterror;
        }
        #endregion

        #region Search
        public async ValueTask<SimilarTpl?> Search(string title, string original_title, int serial)
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title))
                return null;

            string uri = $"{apihost}/api/short?api_token={token}&title={HttpUtility.UrlEncode(original_title ?? title)}";

            string json = await onget.Invoke(uri, apihost);
            if (json == null)
            {
                requesterror?.Invoke();
                return null;
            }

            SearchRoot root = null;

            try
            {
                root = JsonSerializer.Deserialize<SearchRoot>(json);
                if (root?.data == null || root.data.Length == 0)
                    return null;
            }
            catch { return null; }

            var stpl = new SimilarTpl(root.data.Length);

            string enc_title = HttpUtility.UrlEncode(title);
            string enc_original_title = HttpUtility.UrlEncode(original_title);

            foreach (var item in root.data)
            {
                if (item.kp_id == 0 && string.IsNullOrEmpty(item.imdb_id))
                    continue;

                if (serial != -1)
                {
                    if ((serial == 0 && item.content_type != "movie") || (serial == 1 && item.content_type == "movie"))
                        continue;
                }

                bool isok = title != null && title.Length > 3 && item.title != null && item.title.ToLower().Contains(title.ToLower());
                isok = isok ? true : original_title != null && original_title.Length > 3 && item.orig_title != null && item.orig_title.ToLower().Contains(original_title.ToLower());

                if (!isok)
                    continue;

                string year = item.add?.Split("-")?[0] ?? string.Empty;
                string name = !string.IsNullOrEmpty(item.title) && !string.IsNullOrEmpty(item.orig_title) ? $"{item.title} / {item.orig_title}" : (item.title ?? item.orig_title);

                string details = $"imdb: {item.imdb_id} {stpl.OnlineSplit} kinopoisk: {item.kp_id}";

                stpl.Append(name, year, details, host + $"lite/vcdn?title={enc_title}&original_title={enc_original_title}&kinopoisk_id={item.kp_id}&imdb_id={item.imdb_id}");
            }

            return stpl;
        }
        #endregion

        #region Embed
        public async ValueTask<EmbedModel> Embed(long kinopoisk_id, string imdb_id)
        {
            string args = kinopoisk_id > 0 ? $"kp_id={kinopoisk_id}&imdb_id={imdb_id}" : $"imdb_id={imdb_id}";
            string content = await onget.Invoke($"{iframeapihost}?{args}", "https://kinogo.ec/113447-venom-3-poslednij-tanec.html");
            if (content == null)
            {
                requesterror?.Invoke();
                return null;
            }

            var result = new EmbedModel();
            result.type = Regex.Match(content, "id=\"videoType\" value=\"([^\"]+)\"").Groups[1].Value;
            result.voices = new Dictionary<string, string>();

            if (content.Contains("</option>"))
            {
                result.voices.TryAdd("0", "По умолчанию");

                var match = new Regex("<option +value=\"([0-9]+)\"[^>]+>([^<]+)</option>").Match(Regex.Replace(content, "[\n\r\t]+", ""));
                while (match.Success)
                {
                    string translation_id = match.Groups[1].Value;
                    string translation = match.Groups[2].Value.Trim();

                    if (!string.IsNullOrEmpty(translation_id) && !string.IsNullOrEmpty(translation))
                        result.voices.TryAdd(translation_id, translation);

                    match = match.NextMatch();
                }
            }

            string Decode(string pass, string src)
            {
                try
                {
                    int passLen = pass.Length;
                    int srcLen = src.Length;
                    byte[] passArr = new byte[passLen];

                    for (int i = 0; i < passLen; i++)
                    {
                        passArr[i] = (byte)pass[i];
                    }

                    StringBuilder res = new StringBuilder();

                    for (int i = 0; i < srcLen; i += 2)
                    {
                        string hex = src.Substring(i, 2);
                        int code = Convert.ToInt32(hex, 16);
                        byte secret = (byte)(passArr[(i / 2) % passLen] % 255);
                        res.Append((char)(code ^ secret));
                    }

                    return res.ToString();
                }
                catch { return null; }
            }

            string files = null;
            string client_id = Regex.Match(content, "id=\"client_id\" value=\"([^\"]+)\"").Groups[1].Value;

            var m = Regex.Match(content, "<input type=\"hidden\" id=\"[^\"]+\" value=('|\")([^\"']+)");
            while (m.Success)
            {
                string sentry_id = m.Groups[2].Value;
                if (200 > sentry_id.Length || sentry_id.StartsWith("{"))
                {
                    m = m.NextMatch();
                    continue;
                }

                files = Decode(client_id, sentry_id);
                if (!string.IsNullOrEmpty(files))
                    break;

                m = m.NextMatch();
            }

            if (string.IsNullOrEmpty(files))
            {
                files = Regex.Match(content, "value='(\\{\"[0-9]+\"[^\']+)'").Groups[1].Value;
                if (string.IsNullOrEmpty(files))
                    return null;
            }    

            result.quality = files.Contains("1080p") ? "1080p" : files.Contains("720p") ? "720p" : "480p";

            try
            {
                if (result.type is "movie" or "anime")
                {
                    result.movie = JsonSerializer.Deserialize<Dictionary<string, string>>(files);
                    if (result.movie == null)
                        return null;
                }
                else
                {
                    result.serial = JsonSerializer.Deserialize<Dictionary<string, List<Season>>>(files);
                    if (result.serial == null)
                        return null;

                    #region voiceSeasons
                    result.voiceSeasons = new Dictionary<string, HashSet<int>>();

                    foreach (var voice in result.serial.OrderByDescending(k => k.Key == "0"))
                    {
                        if (result.voices.TryGetValue(voice.Key, out string name) && name != null)
                        {
                            foreach (var season in voice.Value)
                            {
                                if (result.voiceSeasons.TryGetValue(voice.Key, out HashSet<int> _s))
                                {
                                    _s.Add(season.id);
                                }
                                else
                                {
                                    result.voiceSeasons.TryAdd(voice.Key, new HashSet<int>() { season.id });
                                }
                            }
                        }
                    }
                    #endregion
                }
            }
            catch { return null; }

            return result;
        }
        #endregion

        #region Html
        public string Html(EmbedModel result, string imdb_id, long kinopoisk_id, string title, string original_title, string t, int s, bool rjson = false)
        {
            if (result == null)
                return string.Empty;

            if (result.type is "movie" or "anime")
            {
                #region Фильм
                if (result.movie == null || result.movie.Count == 0)
                    return string.Empty;

                var mtpl = new MovieTpl(title, original_title, result.movie.Count);

                foreach (var voice in result.movie)
                {
                    result.voices.TryGetValue(voice.Key, out string name);
                    if (string.IsNullOrEmpty(name))
                    {
                        if (result.movie.Count > 1)
                            continue;

                        name = "По умолчанию";
                    }

                    var streamquality = new StreamQualityTpl();

                    foreach (Match m in Regex.Matches(voice.Value, $"\\[(1080|720|480|360)p?\\]([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))"))
                    {
                        string link = m.Groups[2].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        if (usehls && !link.Contains(".m3u"))
                            link += ":hls:manifest.m3u8";
                        else if (!usehls && link.Contains(".m3u"))
                            link = link.Replace(":hls:manifest.m3u8", "");

                        streamquality.Insert(onstream($"{scheme}:{link}"), $"{m.Groups[1].Value}p");
                    }

                    mtpl.Append(name, streamquality.Firts().link, streamquality: streamquality);
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);

                try
                {
                    if (result.serial == null || result.serial.Count == 0)
                        return string.Empty;

                    if (s == -1)
                    {
                        var seasons = new HashSet<int>();

                        foreach (var voice in result.serial)
                        {
                            foreach (var season in voice.Value)
                                seasons.Add(season.id);
                        }

                        var tpl = new SeasonTpl(result.quality, seasons.Count);

                        foreach (int id in seasons.OrderBy(s => s))
                        {
                            string link = host + $"lite/vcdn?kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&rjson={rjson}&title={enc_title}&original_title={enc_original_title}&s={id}";
                            tpl.Append($"{id} сезон", link, id);
                        }

                        return rjson ? tpl.ToJson() : tpl.ToHtml();
                    }
                    else
                    {
                        #region Перевод
                        var vtpl = new VoiceTpl();

                        foreach (var voice in result.voiceSeasons)
                        {
                            if (!voice.Value.Contains(s))
                                continue;

                            if (result.voices.TryGetValue(voice.Key, out string name) && name != null)
                            {
                                if (string.IsNullOrEmpty(t))
                                    t = voice.Key;

                                vtpl.Append(name, t == voice.Key, host + $"lite/vcdn?kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&rjson={rjson}&title={enc_title}&original_title={enc_original_title}&s={s}&t={voice.Key}");
                            }
                        }
                        #endregion

                        if (string.IsNullOrEmpty(t))
                            t = "0";

                        var season = result.serial[t].First(i => i.id == s);
                        if (season.folder == null)
                            return string.Empty;

                        string sArhc = s.ToString();
                        var etpl = new EpisodeTpl(season.folder.Length);

                        foreach (var episode in season.folder)
                        {
                            var streamquality = new StreamQualityTpl();

                            foreach (Match m in Regex.Matches(episode.file ?? "", $"\\[(1080|720|480|360)p?\\]([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))"))
                            {
                                string link = m.Groups[2].Value;
                                if (string.IsNullOrEmpty(link))
                                    continue;

                                if (usehls && !link.Contains(".m3u"))
                                    link += ":hls:manifest.m3u8";
                                else if (!usehls && link.Contains(".m3u"))
                                    link = link.Replace(":hls:manifest.m3u8", "");

                                streamquality.Insert(onstream($"{scheme}:{link}"), $"{m.Groups[1].Value}p");
                            }

                            string e = episode.id.Split("_")[1];

                            etpl.Append($"{e} серия", title ?? original_title, sArhc, e, streamquality.Firts().link, streamquality: streamquality);
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

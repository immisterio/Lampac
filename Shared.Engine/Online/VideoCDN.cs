using System.Text.RegularExpressions;
using System.Web;
using System.Text.Json;
using Shared.Model.Online.VideoCDN;
using System.Text;
using Shared.Model.Templates;
using Lampac.Models.LITE;

namespace Shared.Engine.Online
{
    public class VideoCDNInvoke
    {
        #region VideoCDNInvoke
        string? host, scheme;
        string iframeapihost;
        string apihost;
        string? token;
        bool usehls;
        Func<string, string, ValueTask<string?>> onget;
        Func<string, string>? onstreamfile;
        Func<string, string>? onlog;
        Action? requesterror;

        public string onstream(string stream)
        {
            if (onstreamfile == null)
                return stream;

            return onstreamfile.Invoke(stream);
        }

        public VideoCDNInvoke(OnlinesSettings init, Func<string, string, ValueTask<string?>> onget, Func<string, string>? onstreamfile, string? host = null, Func<string, string>? onlog = null, Action? requesterror = null)
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
        public async ValueTask<string?> Search(string title, string? original_title, int serial)
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title))
                return null;

            string uri = $"{apihost}/api/short?api_token={token}&title={HttpUtility.UrlEncode(original_title ?? title)}";

            string? json = await onget.Invoke(uri, apihost);
            if (json == null)
            {
                requesterror?.Invoke();
                return null;
            }

            var root = JsonSerializer.Deserialize<SearchRoot>(json);
            if (root?.data == null || root.data.Count == 0)
                return null;

            var stpl = new SimilarTpl(root.data.Count);

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

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
                string? name = !string.IsNullOrEmpty(item.title) && !string.IsNullOrEmpty(item.orig_title) ? $"{item.title} / {item.orig_title}" : (item.title ?? item.orig_title);

                string details = $"imdb: {item.imdb_id} {stpl.OnlineSplit} kinopoisk: {item.kp_id}";

                stpl.Append(name, year, details, host + $"lite/vcdn?title={enc_title}&original_title={enc_original_title}&kinopoisk_id={item.kp_id}&imdb_id={item.imdb_id}");
            }

            return stpl.ToHtml();
        }
        #endregion

        #region Embed
        public async ValueTask<EmbedModel?> Embed(long kinopoisk_id, string? imdb_id)
        {
            string args = kinopoisk_id > 0 ? $"kp_id={kinopoisk_id}&imdb_id={imdb_id}" : $"imdb_id={imdb_id}";
            string? content = await onget.Invoke($"{iframeapihost}?{args}", "https://kinogo.biz/82065-pchelovod.html");
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

            string files = Regex.Match(content, "id=\"[^\"]+\" value='(\\{[^\n\r]+)'>").Groups[1].Value;
            result.quality = files.Contains("1080p") ? "1080p" : files.Contains("720p") ? "720p" : "480p";

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
                    if (result.voices.TryGetValue(voice.Key, out string? name) && name != null)
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

            return result;
        }
        #endregion

        #region Html
        public string Html(EmbedModel? result, string? imdb_id, long kinopoisk_id, string? title, string? original_title, string t, int s)
        {
            if (result == null)
                return string.Empty;

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            if (result.type is "movie" or "anime")
            {
                #region Фильм
                if (result.movie == null || result.movie.Count == 0)
                    return string.Empty;

                var mtpl = new MovieTpl(title, original_title, result.movie.Count);

                foreach (var voice in result.movie)
                {
                    result.voices.TryGetValue(voice.Key, out string? name);
                    if (string.IsNullOrEmpty(name))
                    {
                        if (result.movie.Count > 1)
                            continue;

                        name = "По умолчанию";
                    }

                    var streams = new List<(string link, string quality)>() { Capacity = 4 };

                    foreach (Match m in Regex.Matches(voice.Value, $"\\[(1080|720|480|360)p?\\]([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))"))
                    {
                        string link = m.Groups[2].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        if (usehls && !link.Contains(".m3u"))
                            link += ":hls:manifest.m3u8";
                        else if (!usehls && link.Contains(".m3u"))
                            link = link.Replace(":hls:manifest.m3u8", "");

                        streams.Insert(0, (onstream($"{scheme}:{link}"), $"{m.Groups[1].Value}p"));
                    }

                    if (streams.Count == 0)
                        continue;

                    mtpl.Append(name, streams[0].link, streamquality: new StreamQualityTpl(streams));
                }

                return mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                string? enc_title = HttpUtility.UrlEncode(title);
                string? enc_original_title = HttpUtility.UrlEncode(original_title);

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

                        var tpl = new SeasonTpl(result.quality);

                        foreach (int id in seasons.OrderBy(s => s))
                        {
                            string link = host + $"lite/vcdn?kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&title={enc_title}&original_title={enc_original_title}&s={id}";
                            tpl.Append($"{id} сезон", link);
                        }

                        return tpl.ToHtml();
                    }
                    else
                    {
                        #region Перевод
                        foreach (var voice in result.voiceSeasons)
                        {
                            if (!voice.Value.Contains(s))
                                continue;

                            if (result.voices.TryGetValue(voice.Key, out string? name) && name != null)
                            {
                                if (string.IsNullOrEmpty(t))
                                    t = voice.Key;

                                string link = host + $"lite/vcdn?kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&title={enc_title}&original_title={enc_original_title}&s={s}&t={voice.Key}";

                                html.Append("<div class=\"videos__button selector " + (t == voice.Key ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + name + "</div>");
                            }
                        }

                        html.Append("</div><div class=\"videos__line\">");
                        #endregion

                        if (string.IsNullOrEmpty(t))
                            t = "0";

                        var season = result.serial[t].First(i => i.id == s);
                        if (season.folder == null)
                            return string.Empty;

                        foreach (var episode in season.folder)
                        {
                            var streams = new List<(string link, string quality)>() { Capacity = 4 };
                            foreach (Match m in Regex.Matches(episode.file ?? "", $"\\[(1080|720|480|360)p?\\]([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))"))
                            {
                                string link = m.Groups[2].Value;
                                if (string.IsNullOrEmpty(link))
                                    continue;

                                if (usehls && !link.Contains(".m3u"))
                                    link += ":hls:manifest.m3u8";
                                else if (!usehls && link.Contains(".m3u"))
                                    link = link.Replace(":hls:manifest.m3u8", "");

                                streams.Insert(0, (onstream($"{scheme}:{link}"), $"{m.Groups[1].Value}p"));
                            }

                            if (streams.Count == 0)
                                continue;

                            string streansquality = "\"quality\": {" + string.Join(",", streams.Select(s => $"\"{s.quality}\":\"{s.link}\"")) + "}";

                            string e = episode.id.Split("_")[1];
                            html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + e + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + $"{title ?? original_title} ({e} серия)" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{e} серия" + "</div></div>");
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

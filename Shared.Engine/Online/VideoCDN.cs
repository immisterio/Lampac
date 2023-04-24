using System.Text.RegularExpressions;
using System.Web;
using System.Text.Json;
using Shared.Model.Online.VideoCDN;
using System.Text;

namespace Shared.Engine.Online
{
    public class VideoCDNInvoke
    {
        #region VideoCDNInvoke
        string? host;
        string apihost;
        Func<string, string, ValueTask<string?>> onget;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public VideoCDNInvoke(string? host, string apihost, Func<string, string, ValueTask<string?>> onget, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
        }
        #endregion

        #region Embed
        public async ValueTask<EmbedModel?> Embed(long kinopoisk_id, string? imdb_id)
        {
            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return null;

            string args = kinopoisk_id > 0 ? $"kp_id={kinopoisk_id}&imdb_id={imdb_id}" : $"imdb_id={imdb_id}";
            string? content = await onget.Invoke($"{apihost}?{args}", "https://kinogo.biz/53104-avatar-2-2022.html");
            if (content == null)
                return null;

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

            string files = Regex.Match(content, "id=\"files\" value='([^\n\r]+)'>").Groups[1].Value;

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
        public string Html(EmbedModel result, string? imdb_id, long kinopoisk_id, string? title, string? original_title, string t, int s)
        {
            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            if (result.type is "movie" or "anime")
            {
                #region Фильм
                foreach (var voice in result.movie)
                {
                    if (result.voices.TryGetValue(voice.Key, out string name))
                    {
                        var streams = new List<(string link, string quality)>() { Capacity = 4 };

                        foreach (Match m in Regex.Matches(voice.Value, $"\\[(1080|720|480|360)p?\\]([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))"))
                        {
                            string link = m.Groups[2].Value;
                            if (string.IsNullOrEmpty(link))
                                continue;

                            streams.Insert(0, (onstreamfile.Invoke($"https:{link}"), $"{m.Groups[1].Value}p"));
                        }

                        if (streams.Count == 0)
                            continue;

                        string streansquality = "\"quality\": {" + string.Join(",", streams.Select(s => $"\"{s.quality}\":\"{s.link}\"")) + "}";

                        html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + (title ?? original_title) + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + name + "</div></div>");
                        firstjson = false;
                    }
                }
                #endregion
            }
            else
            {
                #region Сериал
                string? enc_title = HttpUtility.UrlEncode(title);
                string? enc_original_title = HttpUtility.UrlEncode(original_title);

                try
                {
                    if (s == -1)
                    {
                        var seasons = new HashSet<int>();

                        foreach (var voice in result.serial)
                        {
                            foreach (var season in voice.Value)
                                seasons.Add(season.id);
                        }

                        foreach (int id in seasons.OrderBy(s => s))
                        {
                            string link = host + $"lite/vcdn?kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&title={enc_title}&original_title={enc_original_title}&s={id}";

                            html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{id} сезон" + "</div></div></div>");
                            firstjson = false;
                        }
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

                                streams.Insert(0, (onstreamfile.Invoke($"https:{link}"), $"{m.Groups[1].Value}p"));
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

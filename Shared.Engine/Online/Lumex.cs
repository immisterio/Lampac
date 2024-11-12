using System.Text.RegularExpressions;
using System.Web;
using System.Text.Json;
using Shared.Model.Online.Lumex;
using Shared.Model.Templates;
using Lampac.Models.LITE;

namespace Shared.Engine.Online
{
    public class LumexInvoke
    {
        #region LumexInvoke
        string? host, scheme;
        string iframeapihost;
        string apihost;
        string? token;
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

        public LumexInvoke(OnlinesSettings init, Func<string, string, ValueTask<string?>> onget, Func<string, string>? onstreamfile, string? host = null, Func<string, string>? onlog = null, Action? requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.scheme = init.scheme;
            this.iframeapihost = init.corsHost();
            this.apihost = init.cors(init.apihost);
            this.token = init!.token;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.requesterror = requesterror;
        }
        #endregion

        #region Search
        public async ValueTask<SimilarTpl?> Search(string title, string? original_title, int serial)
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

            SearchRoot? root = null;

            try
            {
                root = JsonSerializer.Deserialize<SearchRoot>(json);
                if (root?.data == null || root.data.Count == 0)
                    return null;
            }
            catch { return null; }

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

            return stpl;
        }
        #endregion

        #region Html
        public string Html(EmbedModel? result, string? imdb_id, long kinopoisk_id, string? title, string? original_title, string t, int s, bool rjson = false)
        {
            if (result == null)
                return string.Empty;

            if (result.content_type is "movie" or "anime")
            {
                #region Фильм
                if (result.media == null || result.media.Count == 0)
                    return string.Empty;

                var mtpl = new MovieTpl(title, original_title, result.media.Count);

                foreach (var media in result.media)
                {
                    var subtitles = new SubtitleTpl();
                    if (media.subtitles != null && media.subtitles.Count > 0)
                    {
                        foreach (string srt in media.subtitles)
                        {
                            string name = Regex.Match(srt, "/([^\\.]+)\\.srt").Groups[1].Value;
                            subtitles.Append(name, onstream($"http:{srt}"));
                        }
                    }

                    mtpl.Append(media.translation_name, host+$"lite/lumex/video.m3u8?playlist={HttpUtility.UrlEncode(media.playlist)}&csrf={result.csrf}", subtitles: subtitles);
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                return string.Empty;

                #region Сериал
                //string? enc_title = HttpUtility.UrlEncode(title);
                //string? enc_original_title = HttpUtility.UrlEncode(original_title);

                //try
                //{
                //    if (result.serial == null || result.serial.Count == 0)
                //        return string.Empty;

                //    if (s == -1)
                //    {
                //        var seasons = new HashSet<int>();

                //        foreach (var voice in result.serial)
                //        {
                //            foreach (var season in voice.Value)
                //                seasons.Add(season.id);
                //        }

                //        var tpl = new SeasonTpl(result.quality);

                //        foreach (int id in seasons.OrderBy(s => s))
                //        {
                //            string link = host + $"lite/vcdn?kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&rjson={rjson}&title={enc_title}&original_title={enc_original_title}&s={id}";
                //            tpl.Append($"{id} сезон", link, id);
                //        }

                //        return rjson ? tpl.ToJson() : tpl.ToHtml();
                //    }
                //    else
                //    {
                //        #region Перевод
                //        var vtpl = new VoiceTpl();

                //        foreach (var voice in result.voiceSeasons)
                //        {
                //            if (!voice.Value.Contains(s))
                //                continue;

                //            if (result.voices.TryGetValue(voice.Key, out string? name) && name != null)
                //            {
                //                if (string.IsNullOrEmpty(t))
                //                    t = voice.Key;

                //                vtpl.Append(name, t == voice.Key, host + $"lite/vcdn?kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&rjson={rjson}&title={enc_title}&original_title={enc_original_title}&s={s}&t={voice.Key}");
                //            }
                //        }
                //        #endregion

                //        if (string.IsNullOrEmpty(t))
                //            t = "0";

                //        var season = result.serial[t].First(i => i.id == s);
                //        if (season.folder == null)
                //            return string.Empty;

                //        var etpl = new EpisodeTpl();

                //        foreach (var episode in season.folder)
                //        {
                //            var streams = new List<(string link, string quality)>() { Capacity = 4 };
                //            foreach (Match m in Regex.Matches(episode.file ?? "", $"\\[(1080|720|480|360)p?\\]([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))"))
                //            {
                //                string link = m.Groups[2].Value;
                //                if (string.IsNullOrEmpty(link))
                //                    continue;

                //                streams.Insert(0, (onstream($"{scheme}:{link}"), $"{m.Groups[1].Value}p"));
                //            }

                //            if (streams.Count == 0)
                //                continue;

                //            string e = episode.id.Split("_")[1];

                //            etpl.Append($"{e} серия", title ?? original_title, s.ToString(), e, streams[0].link, streamquality: new StreamQualityTpl(streams));
                //        }

                //        if (rjson)
                //            return etpl.ToJson(vtpl);

                //        return vtpl.ToHtml() + etpl.ToHtml();
                //    }
                //}
                //catch
                //{
                //    return string.Empty;
                //}
                #endregion
            }
        }
        #endregion
    }
}

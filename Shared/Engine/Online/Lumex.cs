using Shared.Models.Base;
using Shared.Models.Online.Lumex;
using Shared.Models.Online.Settings;
using Shared.Models.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public struct LumexInvoke
    {
        #region LumexInvoke
        string host, scheme;
        bool hls;
        string apihost;
        string token;
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

        public LumexInvoke(LumexSettings init, Func<string, string, ValueTask<string>> onget, Func<string, string> onstreamfile, string host = null, Func<string, string> onlog = null, Action requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.scheme = init.scheme ?? "http";
            this.hls = init.hls;
            this.apihost = init.cors(init.apihost);
            this.token = init!.token;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.requesterror = requesterror;
        }
        #endregion

        #region Search
        public async ValueTask<SimilarTpl> Search(string title, string original_title, int serial, int clarification, IEnumerable<DatumDB> database = null)
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title))
                return default;

            string enc_title = HttpUtility.UrlEncode(title);
            string enc_original_title = HttpUtility.UrlEncode(original_title);

            if (!string.IsNullOrEmpty(token))
            {
                #region api/short
                string uri = $"{apihost}/api/short?api_token={token}&title={HttpUtility.UrlEncode(clarification == 1 ? title : (original_title ?? title))}";

                string json = await onget.Invoke(uri, apihost);
                if (json == null)
                {
                    requesterror?.Invoke();
                    return default;
                }

                SearchRoot root = null;

                try
                {
                    root = JsonSerializer.Deserialize<SearchRoot>(json);
                    if (root?.data == null || root.data.Length == 0)
                        return default;
                }
                catch { return default; }

                var stpl = new SimilarTpl(root.data.Length);

                foreach (var item in root.data)
                {
                    if (serial != -1)
                    {
                        if ((serial == 0 && item.content_type != "movie") || (serial == 1 && item.content_type == "movie"))
                            continue;
                    }

                    if (clarification != 1)
                    {
                        bool isok = title != null && title.Length > 3 && item.title != null && item.title.ToLower().Contains(title.ToLower());
                        isok = isok ? true : original_title != null && original_title.Length > 3 && item.orig_title != null && item.orig_title.ToLower().Contains(original_title.ToLower());

                        if (!isok)
                            continue;
                    }

                    string year = item.add?.Split("-")?[0] ?? string.Empty;
                    string name = !string.IsNullOrEmpty(item.title) && !string.IsNullOrEmpty(item.orig_title) ? $"{item.title} / {item.orig_title}" : (item.title ?? item.orig_title);

                    string details = $"imdb: {item.imdb_id} {stpl.OnlineSplit} kinopoisk: {item.kp_id}";

                    string img = PosterApi.Find(item.kp_id, item.imdb_id);
                    stpl.Append(name, year, details, host + $"lite/lumex?title={enc_title}&original_title={enc_original_title}&content_type={item.content_type}&content_id={item.id}&clarification={clarification}", img);
                }

                return stpl;
                #endregion
            }
            else if (database != null)
            {
                #region database
                int capacity = 100;
                if (database is ICollection<DatumDB> collection)
                    capacity = collection.Count > 100 ? 100 : collection.Count;

                var stpl = new SimilarTpl(capacity);

                foreach (var item in database)
                {
                    if (stpl.data.Count >= 100)
                        break;

                    if (item.kinopoisk_id == 0 && string.IsNullOrEmpty(item.imdb_id))
                        continue;

                    if (serial != -1)
                    {
                        if ((serial == 0 && item.content_type != "movie") || (serial == 1 && item.content_type == "movie"))
                            continue;
                    }

                    bool isok = false;

                    if (StringConvert.SearchName(original_title) != null)
                    {
                        if (StringConvert.SearchName(item.orig_title) == StringConvert.SearchName(original_title))
                            isok = true;
                    }

                    string stitle = StringConvert.SearchName(title);
                    if (!isok && stitle != null)
                    {
                        if (!string.IsNullOrEmpty(item.ru_title))
                        {
                            if (StringConvert.SearchName(item.ru_title, string.Empty)!.Contains(stitle))
                                isok = true;
                        }

                        if (!isok && StringConvert.SearchName(item.orig_title) != null && stitle != null)
                        {
                            if (StringConvert.SearchName(item.orig_title)!.Contains(stitle))
                                isok = true;
                        }
                    }

                    if (!isok)
                        continue;

                    string year = item.year?.Split("-")?[0] ?? string.Empty;
                    string name = !string.IsNullOrEmpty(item.ru_title) && !string.IsNullOrEmpty(item.orig_title) ? $"{item.ru_title} / {item.orig_title}" : (item.ru_title ?? item.orig_title);

                    string details = $"imdb: {item.imdb_id} {stpl.OnlineSplit} kinopoisk: {item.kinopoisk_id}";

                    string img = PosterApi.Find(item.kinopoisk_id, item.imdb_id);
                    stpl.Append(name, year, details, host + $"lite/lumex?title={enc_title}&original_title={enc_original_title}&content_type={item.content_type}&content_id={item.id}&clarification={clarification}", img);
                }

                return stpl;
                #endregion
            }

            return default;
        }
        #endregion

        #region Html
        public string Html(EmbedModel result, string args, long content_id, string content_type, string imdb_id, long kinopoisk_id, string title, string original_title, int clarification, string t, int s, bool rjson = false, bool bwa = false)
        {
            if (result?.media == null || result.media.Length == 0)
                return string.Empty;

            if (!string.IsNullOrEmpty(args))
                args = $"&{args.Remove(0, 1)}";

            if (result.content_type is "movie" or "anime")
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, result.media.Length);

                foreach (var media in result.media)
                {
                    var subtitles = new SubtitleTpl(media.subtitles?.Length ?? 0);
                    if (media.subtitles != null && media.subtitles.Length > 0)
                    {
                        foreach (string srt in media.subtitles)
                        {
                            string name = Regex.Match(srt, "/([^\\.\\/]+)\\.srt").Groups[1].Value;
                            subtitles.Append(name, onstream($"{scheme}:{srt}"));
                        }
                    }

                    string link = host + $"lite/lumex/video.m3u8?playlist={HttpUtility.UrlEncode(media.playlist)}&csrf={result.csrf}&max_quality={media.max_quality}{args}";

                    if (bwa || !hls)
                    {
                        mtpl.Append(media.translation_name, link.Replace(".m3u8", ""), "call", subtitles: subtitles, quality: media.max_quality?.ToString());
                    }
                    else
                    {
                        mtpl.Append(media.translation_name, link, subtitles: subtitles, quality: media.max_quality?.ToString());
                    }
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
                    if (s == -1)
                    {
                        var tpl = new SeasonTpl(result.media.Length);

                        foreach (var media in result.media.OrderBy(s => s.season_id))
                        {
                            string link = host + $"lite/lumex?content_id={content_id}&content_type={content_type}&kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&rjson={rjson}&title={enc_title}&original_title={enc_original_title}&clarification={clarification}&s={media.season_id}{args}";    

                            tpl.Append($"{media.season_id} сезон", link, media.season_id);
                        }

                        return rjson ? tpl.ToJson() : tpl.ToHtml();
                    }
                    else
                    {
                        #region Перевод
                        var vtpl = new VoiceTpl();
                        var tmpVoice = new HashSet<int>();

                        foreach (var media in result.media)
                        {
                            if (media.season_id != s)
                                continue;

                            foreach (var episode in media.episodes)
                            {
                                foreach (var voice in episode.media)
                                {
                                    if (tmpVoice.Contains(voice.translation_id))
                                        continue;

                                    tmpVoice.Add(voice.translation_id);

                                    if (string.IsNullOrEmpty(t))
                                        t = voice.translation_id.ToString();

                                    vtpl.Append(voice.translation_name, t == voice.translation_id.ToString(), host + $"lite/lumex?content_id={content_id}&content_type={content_type}&kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&rjson={rjson}&title={enc_title}&original_title={enc_original_title}&clarification={clarification}&s={s}&t={voice.translation_id}");
                                }
                            }
                        }
                        #endregion

                        if (string.IsNullOrEmpty(t))
                            t = "0";

                        var etpl = new EpisodeTpl();
                        string sArhc = s.ToString();

                        foreach (var media in result.media)
                        {
                            if (media.season_id != s)
                                continue;

                            foreach (var episode in media.episodes)
                            {
                                foreach (var voice in episode.media)
                                {
                                    if (voice.translation_id.ToString() != t)
                                        continue;

                                    var subtitles = new SubtitleTpl(media.subtitles?.Length ?? 0);
                                    if (media.subtitles != null && media.subtitles.Length > 0)
                                    {
                                        foreach (string srt in media.subtitles)
                                        {
                                            string name = Regex.Match(srt, "/([^\\.\\/]+)\\.srt").Groups[1].Value;
                                            subtitles.Append(name, onstream($"{scheme}:{srt}"));
                                        }
                                    }

                                    string link = host + $"lite/lumex/video.m3u8?playlist={HttpUtility.UrlEncode(voice.playlist)}&csrf={result.csrf}&max_quality={voice.max_quality}{args}";

                                    if (bwa || !hls)
                                    {
                                        etpl.Append($"{episode.episode_id} серия", title ?? original_title, sArhc, episode.episode_id.ToString(), link.Replace(".m3u8", ""), "call", subtitles: subtitles);
                                    }
                                    else
                                    {
                                        etpl.Append($"{episode.episode_id} серия", title ?? original_title, sArhc, episode.episode_id.ToString(), link, subtitles: subtitles);
                                    }
                                }
                            }
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

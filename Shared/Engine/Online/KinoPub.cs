using Shared.Models.Base;
using Shared.Models.Online.KinoPub;
using Shared.Models.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class KinoPubInvoke
    {
        #region KinoPubInvoke
        string host, token;
        string apihost;
        Func<string, ValueTask<string>> onget;
        Func<string, string, string> onstreamfile;
        Func<string, string> onlog;
        Action requesterror;

        public KinoPubInvoke(string host, string apihost, string token, Func<string, ValueTask<string>> onget, Func<string, string, string> onstreamfile, Func<string, string> onlog = null, Action requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.token = token;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.requesterror = requesterror;
        }
        #endregion

        #region Search
        async public Task<SearchResult> Search(string title, string original_title, int year, int clarification, string imdb_id, long kinopoisk_id)
        {
            if (string.IsNullOrEmpty(title ?? original_title))
                return null;

            string enc_title = HttpUtility.UrlEncode(title);
            string enc_original_title = HttpUtility.UrlEncode(original_title);

            #region goSearch
            async Task<SearchResult> goSearch(string q)
            {
                if (string.IsNullOrEmpty(q))
                    return null;

                string json = await onget($"{apihost}/v1/items/search?q={HttpUtility.UrlEncode(q)}&access_token={token}&field=title&perpage=200");
                if (json == null)
                {
                    requesterror?.Invoke();
                    return null;
                }

                try
                {
                    var items = JsonSerializer.Deserialize<SearchObject>(json)?.items;
                    if (items != null)
                    {
                        var ids = new List<int>(items.Length);
                        var result = new SearchResult() { similars = new SimilarTpl(items.Length) };

                        string _q = StringConvert.SearchName(q);

                        foreach (var item in items)
                        {
                            string img = PosterApi.Size(item.posters?.Skip(1)?.First().Value);
                            result.similars.Value.Append(item.title, item.year.ToString(), item.voice, host + $"lite/kinopub?postid={item.id}&title={enc_title}&original_title={enc_original_title}", img);

                            if ((item.kinopoisk > 0 && item.kinopoisk == kinopoisk_id) || $"tt{item.imdb}" == imdb_id)
                            {
                                if (item.type != "3d")
                                    result.id = item.id;
                            }
                            else
                            {
                                if (item.year == year || (item.year == year - 1) || (item.year == year + 1))
                                {
                                    string _t = StringConvert.SearchName(item.title);

                                    if (!string.IsNullOrEmpty(_t) && !string.IsNullOrEmpty(_q))
                                    {
                                        if (_t.StartsWith(_q) || _t.EndsWith(_q))
                                            ids.Add(item.id);
                                    }
                                }
                            }
                        }

                        if (ids.Count == 1 && result.id == 0)
                            result.id = ids[0];

                        return result;
                    }
                }
                catch { }

                return null;
            }
            #endregion

            if (clarification == 1)
                return await goSearch(title);

            return (await goSearch(original_title)) ?? (await goSearch(title));
        }
        #endregion

        #region Post
        async public Task<RootObject> Post(int postid)
        {
            string json = await onget($"{apihost}/v1/items/{postid}?access_token={token}");
            if (json == null)
            {
                requesterror?.Invoke();
                return null;
            }

            try
            {
                var root = JsonSerializer.Deserialize<RootObject>(json);
                if (root?.item.seasons == null && root?.item.videos == null)
                    return null;

                return root;
            }
            catch { return null; }
        }
        #endregion

        #region Html
        public string Html(RootObject root, string filetype, string title, string original_title, int postid, int s = -1, int t = -1, string codec = null, VastConf vast = null, bool rjson = false)
        {
            if (root == null)
                return string.Empty;

            if (root?.item.videos != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, root.item.videos.Length);

                if (filetype == "hls")
                {
                    foreach (var a in root.item.videos[0].audios)
                    {
                        var streamquality = new StreamQualityTpl();

                        foreach (var f in root.item.videos[0].files)
                        {
                            if (!string.IsNullOrEmpty(f.url.hls))
                                streamquality.Append(onstreamfile(f.url.hls.Replace("a1.m3u8", $"a{a.index}.m3u8"), null), f.quality);
                        }

                        if (!streamquality.Any())
                            continue;

                        string voice = a.type?.title ?? a.lang ?? "оригинал";
                        if (!string.IsNullOrEmpty(a?.author?.title))
                            voice += $" ({a.author.title})";

                        #region subtitle
                        var subtitles = new SubtitleTpl(root.item.videos[0]?.subtitles?.Length ?? 0);

                        if (root.item.videos[0].subtitles != null)
                        {
                            foreach (var sub in root.item.videos[0].subtitles)
                            {
                                if (sub.url != null)
                                    subtitles.Append(sub.lang, onstreamfile(sub.url, null));
                            }
                        }

                        string subtitles_call = null;
                        if (subtitles.IsEmpty())
                            subtitles_call = host + $"lite/kinopub/subtitles.json?mid={root.item.videos[0].id}";
                        #endregion

                        mtpl.Append(voice, streamquality.Firts().link, streamquality: streamquality, subtitles: subtitles, subtitles_call: subtitles_call, voice_name: a.codec, vast: vast);
                    }
                }
                else
                {
                    foreach (var v in root.item.videos)
                    {
                        #region voicename
                        string voicename = string.Empty;

                        if (v.audios != null)
                        {
                            foreach (var audio in v.audios)
                            {
                                if (audio.lang == "eng")
                                {
                                    if (!voicename.Contains(audio.lang))
                                        voicename += "eng, ";
                                }
                                else
                                {
                                    string a = audio?.author?.title ?? audio?.type?.title;
                                    if (a != null)
                                    {
                                        a = $"{a} ({audio.lang})";
                                        if (!voicename.Contains(a))
                                            voicename += $"{a}, ";
                                    }
                                }
                            }

                            voicename = Regex.Replace(voicename, "[, ]+$", "");
                        }
                        #endregion

                        if (filetype == "hls4")
                        {
                            if (v.files[0].url.hls4 != null)
                                mtpl.Append(v.files[0].quality, onstreamfile(v.files[0].url.hls4, null), voice_name: voicename, vast: vast);
                        }
                        else
                        {
                            if (v.files[0].url.http == null)
                                continue;

                            #region subtitle
                            var subtitles = new SubtitleTpl(v.subtitles?.Length ?? 0);

                            if (v.subtitles != null)
                            {
                                foreach (var sub in v.subtitles)
                                {
                                    if (sub.url != null)
                                        subtitles.Append(sub.lang, onstreamfile(sub.url, null));
                                }
                            }

                            string subtitles_call = null;
                            if (subtitles.IsEmpty())
                                subtitles_call = host + $"lite/kinopub/subtitles.json?mid={v.id}";
                            #endregion

                            var streamquality = new StreamQualityTpl(v.files.Select(f => (onstreamfile(f.url.http, f.file), f.quality)));
                            var first = streamquality.Firts();

                            mtpl.Append(first.quality, first.link, subtitles: subtitles, subtitles_call: subtitles_call, voice_name: voicename, streamquality: streamquality, vast: vast);
                        }
                    }
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                if (root?.item.seasons == null || root.item.seasons.Length == 0)
                    return string.Empty;

                #region Сериал
                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);

                if (s == -1)
                {
                    #region Сезоны
                    var tpl = new SeasonTpl(root.item.quality > 0 ? $"{root.item.quality}p" : null, root.item.seasons.Length);

                    foreach (var season in root.item.seasons)
                    {
                        string link = host + $"lite/kinopub?rjson={rjson}&postid={postid}&title={enc_title}&original_title={enc_original_title}&s={season.number}";
                        tpl.Append($"{season.number} сезон", link, season.number);
                    }

                    return rjson ? tpl.ToJson() : tpl.ToHtml();
                    #endregion
                }
                else
                {
                    #region Серии
                    string sArhc = s.ToString();

                    if (filetype == "hls")
                    {
                        #region Перевод
                        var vtpl = new VoiceTpl();
                        var hash = new HashSet<string>();

                        foreach (var a in root.item.seasons.First(i => i.number == s).episodes[0].audios)
                        {
                            string voice = a?.author?.title ?? a?.type?.title;

                            int? idt = a?.author?.id;
                            if (idt == null)
                                idt = a?.type?.id ?? null;

                            if (idt == null)
                            {
                                if (a.lang == "eng")
                                {
                                    idt = 6;
                                    voice = "Оригинал";
                                }
                                else
                                {
                                    idt = 1;
                                    voice = "По умолчанию";
                                }
                            }

                            if (string.IsNullOrEmpty(voice))
                                continue;

                            if (t == -1)
                            {
                                t = (int)idt;
                                codec = a.codec;
                            }

                            if (!hash.Contains($"{voice}:{a.codec}"))
                            {
                                hash.Add($"{voice}:{a.codec}");

                                string link = host + $"lite/kinopub?rjson={rjson}&postid={postid}&title={enc_title}&original_title={enc_original_title}&s={s}&t={idt}&codec={a.codec}";
                                bool active = t == idt && (codec == null || codec == a.codec);

                                vtpl.Append($"{voice} ({a.codec})", active, link);
                            }
                            }
                            #endregion

                        #region Серии
                        var etpl = new EpisodeTpl();

                        foreach (var episode in root.item.seasons.First(i => i.number == s).episodes)
                        {
                            int voice_index = -1;
                            if (t == 1)
                            {
                                voice_index = t;
                            }
                            else
                            {
                                foreach (var a in episode.audios)
                                {
                                    int? idt = a?.author?.id;
                                    if (idt == null)
                                        idt = a?.type?.id;

                                    if ((idt != null && t == (int)idt && (codec == null || codec == a.codec)) ||
                                        (t == 6 && a.lang == "eng"))
                                    {
                                        voice_index = a!.index;
                                        break;
                                    }
                                }

                                if (voice_index == -1)
                                    break;
                            }

                            var streamquality = new StreamQualityTpl();

                            foreach (var f in episode.files)
                            {
                                if (!string.IsNullOrEmpty(f.url.hls))
                                    streamquality.Append(onstreamfile(f.url.hls.Replace("a1.m3u8", $"a{voice_index}.m3u8"), null), f.quality);
                            }

                            #region subtitle
                            var subtitles = new SubtitleTpl(episode.subtitles?.Length ?? 0);

                            if (episode.subtitles != null)
                            {
                                foreach (var sub in episode.subtitles)
                                {
                                    if (sub.url != null)
                                        subtitles.Append(sub.lang, onstreamfile(sub.url, null));
                                }
                            }

                            string subtitles_call = null;
                            if (subtitles.IsEmpty())
                                subtitles_call = host + $"lite/kinopub/subtitles.json?mid={episode.id}";
                            #endregion

                            etpl.Append($"{episode.number} серия", title ?? original_title, sArhc, episode.number.ToString(), streamquality.Firts().link, streamquality: streamquality, subtitles: subtitles, subtitles_call: subtitles_call, vast: vast);
                        }
                        #endregion

                        if (rjson)
                            return etpl.ToJson(vtpl);

                        return vtpl.ToHtml() + etpl.ToHtml();
                    }
                    else
                    {
                        var episodes = root.item.seasons.First(i => i.number == s).episodes;

                        var etpl = new EpisodeTpl(episodes.Length);

                        foreach (var episode in episodes)
                        {
                            #region voicename
                            string voicename = string.Empty;

                            if (episode.audios != null)
                            {
                                foreach (var audio in episode.audios)
                                {
                                    string a = audio.author?.title ?? audio.lang;
                                    if (a != null && !voicename.Contains(a) && a != "rus")
                                        voicename += $"{a}, ";
                                }

                                voicename = Regex.Replace(voicename, "[, ]+$", "");
                            }
                            #endregion

                            if (filetype == "hls4")
                            {
                                if (episode.files[0].url.hls4 == null)
                                    continue;

                                etpl.Append($"{episode.number} серия", title ?? original_title, sArhc, episode.number.ToString(), onstreamfile(episode.files[0].url.hls4, null), voice_name: voicename, vast: vast);
                            }
                            else
                            {
                                if (episode.files[0].url.http == null)
                                    continue;

                                #region subtitle
                                var subtitles = new SubtitleTpl(episode.subtitles?.Length ?? 0);

                                if (episode.subtitles != null)
                                {
                                    foreach (var sub in episode.subtitles)
                                    {
                                        if (sub.url != null)
                                            subtitles.Append(sub.lang, onstreamfile(sub.url, null));
                                    }
                                }

                                string subtitles_call = null;
                                if (subtitles.IsEmpty())
                                    subtitles_call = host + $"lite/kinopub/subtitles.json?mid={episode.id}";
                                #endregion

                                #region streams
                                var streamquality = new StreamQualityTpl();

                                foreach (var f in episode.files)
                                {
                                    if (f.url.http != null)
                                        streamquality.Append(onstreamfile(f.url.http, f.file), f.quality);
                                }
                                #endregion

                                etpl.Append($"{episode.number} серия", title ?? original_title, sArhc, episode.number.ToString(), streamquality.Firts().link, subtitles: subtitles, subtitles_call: subtitles_call, voice_name: voicename, streamquality: streamquality, vast: vast);
                            }
                        }

                        return rjson ? etpl.ToJson() : etpl.ToHtml();
                    }
                    #endregion
                }
                #endregion
            }
        }
        #endregion
    }
}

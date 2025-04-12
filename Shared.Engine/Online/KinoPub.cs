using Lampac.Models.LITE.KinoPub;
using Shared.Model.Base;
using Shared.Model.Online.KinoPub;
using Shared.Model.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class KinoPubInvoke
    {
        #region KinoPubInvoke
        string? host, token;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string?, string> onstreamfile;
        Func<string, string>? onlog;
        Action? requesterror;

        public KinoPubInvoke(string? host, string apihost, string? token, Func<string, ValueTask<string?>> onget, Func<string, string?, string> onstreamfile, Func<string, string>? onlog = null, Action? requesterror = null)
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
        /// <returns>
        /// 0 - Данные не получены
        /// 1 - Нет нужного контента 
        /// </returns>
        async public ValueTask<SearchResult?> Search(string? title, string? original_title, int year, int clarification, string? imdb_id, long kinopoisk_id, bool similar)
        {
            if (string.IsNullOrEmpty(title))
                return null;

            async ValueTask<SearchResult?> goSearch(string q, bool sendrequesterror)
            {
                if (string.IsNullOrEmpty(q))
                    return null;

                string? json = await onget($"{apihost}/v1/items/search?q={HttpUtility.UrlEncode(q)}&access_token={token}&field=title&perpage=200");
                if (json == null)
                {
                    if (sendrequesterror)
                        requesterror?.Invoke();
                    return null;
                }

                try
                {
                    var items = JsonSerializer.Deserialize<SearchObject>(json)?.items;
                    if (items != null)
                    {
                        if (kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id))
                        {
                            var ids = new List<int>();
                            var stpl = new SimilarTpl(items.Count);

                            string? enc_title = HttpUtility.UrlEncode(title);
                            string? enc_original_title = HttpUtility.UrlEncode(original_title);

                            foreach (var item in items)
                            {
                                stpl.Append(item.title, item.year.ToString(), item.voice, host + $"lite/kinopub?postid={item.id}&title={enc_title}&original_title={enc_original_title}", item?.posters?.First().Value);

                                if (item.year == year || (item.year == year - 1) || (item.year == year + 1))
                                {
                                    string _t = item.title.ToLower();
                                    string _s = q.ToLower();

                                    if (item.year == year && (_t.StartsWith(_s) || _t.EndsWith(_s)))
                                        ids.Add(item.id);
                                }
                            }

                            if (ids.Count == 1 && !similar)
                                return new SearchResult() { id = ids[0] };

                            return new SearchResult() { similars = stpl };
                        }
                        else
                        {
                            foreach (var item in items)
                            {
                                if (item.kinopoisk > 0 && item.kinopoisk == kinopoisk_id)
                                    return new SearchResult() { id = item.id };

                                if ($"tt{item.imdb}" == imdb_id)
                                    return new SearchResult() { id = item.id };
                            }

                            return null;
                        }
                    }
                }
                catch { }

                return null;
            }

            if (clarification == 1)
            {
                if (string.IsNullOrEmpty(title))
                    return null;

                return await goSearch(title, true);
            }

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(original_title))
                return await goSearch(original_title ?? title, true);

            return (await goSearch(original_title, false)) ?? (await goSearch(title, true));
        }
        #endregion

        #region Post
        async public ValueTask<RootObject?> Post(int postid)
        {
            string? json = await onget($"{apihost}/v1/items/{postid}?access_token={token}");
            if (json == null)
            {
                requesterror?.Invoke();
                return null;
            }

            try
            {
                var root = JsonSerializer.Deserialize<RootObject>(json);
                if (root?.item?.seasons == null && root?.item?.videos == null)
                    return null;

                return root;
            }
            catch { return null; }
        }
        #endregion

        #region Html
        public string Html(RootObject? root, string? filetype, string? title, string? original_title, int postid, int s = -1, int t = -1, string? codec = null, VastConf? vast = null, bool rjson = false)
        {
            if (root == null)
                return string.Empty;

            if (root?.item?.videos != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, root.item.videos.Count);

                if (filetype == "hls")
                {
                    foreach (var a in root.item.videos[0].audios)
                    {
                        var streams = new List<(string link, string quality)>(4);

                        foreach (var f in root.item.videos[0].files)
                        {
                            if (!string.IsNullOrEmpty(f.url.hls))
                                streams.Add((onstreamfile(f.url.hls.Replace("a1.m3u8", $"a{a.index}.m3u8"), null), f.quality));
                        }

                        if (streams.Count == 0)
                            continue;

                        string voice = a.type?.title ?? a.lang ?? "оригинал";
                        if (!string.IsNullOrEmpty(a?.author?.title))
                            voice += $" ({a.author.title})";

                        #region subtitle
                        var subtitles = new SubtitleTpl();

                        if (root.item.videos[0].subtitles != null)
                        {
                            foreach (var sub in root.item.videos[0].subtitles)
                            {
                                if (sub.url != null)
                                    subtitles.Append(sub.lang, onstreamfile(sub.url, null));
                            }
                        }
                        #endregion

                        mtpl.Append(voice, streams[0].link, streamquality: new StreamQualityTpl(streams), subtitles: subtitles, voice_name: a.codec, vast: vast);
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
                                    string? a = audio?.author?.title ?? audio?.type?.title;
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
                            var subtitles = new SubtitleTpl();

                            if (v.subtitles != null)
                            {
                                foreach (var sub in v.subtitles)
                                {
                                    if (sub.url != null)
                                        subtitles.Append(sub.lang, onstreamfile(sub.url, null));
                                }
                            }
                            #endregion

                            var streamquality = new StreamQualityTpl(v.files.Select(f => (onstreamfile(f.url.http, f.file), f.quality)));
                            mtpl.Append(v.files[0].quality, onstreamfile(v.files[0].url.http, v.files[0].file), subtitles: subtitles, voice_name: voicename, streamquality: streamquality, vast: vast);
                        }
                    }
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                if (root?.item?.seasons == null || root.item.seasons.Count == 0)
                    return string.Empty;

                #region Сериал
                string? enc_title = HttpUtility.UrlEncode(title);
                string? enc_original_title = HttpUtility.UrlEncode(original_title);

                if (s == -1)
                {
                    #region Сезоны
                    var tpl = new SeasonTpl(root.item.quality > 0 ? $"{root.item.quality}p" : null);

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
                    if (filetype == "hls")
                    {
                        #region Перевод
                        var vtpl = new VoiceTpl();

                        foreach (var a in root.item.seasons.First(i => i.number == s).episodes[0].audios)
                        {
                            string? voice = a?.author?.title ?? a?.type?.title;

                            int? idt = a?.author?.id;
                            if (idt == null)
                                idt = a?.type?.id ?? null;

                            if (idt == null)
                            {
                                if (a.lang != "eng")
                                    continue;

                                idt = 6;
                                voice = "Оригинал";
                            }

                            if (string.IsNullOrEmpty(voice))
                                continue;

                            if (t == -1)
                            {
                                t = (int)idt;
                                codec = a.codec;
                            }

                            string link = host + $"lite/kinopub?rjson={rjson}&postid={postid}&title={enc_title}&original_title={enc_original_title}&s={s}&t={idt}&codec={a.codec}";
                            bool active = t == idt && (codec == null || codec == a.codec);

                            vtpl.Append($"{voice} ({a.codec})", active, link);
                        }
                        #endregion

                        #region Серии
                        var etpl = new EpisodeTpl();

                        foreach (var episode in root.item.seasons.First(i => i.number == s).episodes)
                        {
                            int voice_index = -1;
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

                            var streams = new List<(string link, string quality)>(4);

                            foreach (var f in episode.files)
                            {
                                if (!string.IsNullOrEmpty(f.url.hls))
                                    streams.Add((onstreamfile(f.url.hls.Replace("a1.m3u8", $"a{voice_index}.m3u8"), null), f.quality));
                            }

                            if (streams.Count == 0)
                                continue;

                            #region subtitle
                            var subtitles = new SubtitleTpl();

                            if (episode.subtitles != null)
                            {
                                foreach (var sub in episode.subtitles)
                                {
                                    if (sub.url != null)
                                        subtitles.Append(sub.lang, onstreamfile(sub.url, null));
                                }
                            }
                            #endregion

                            etpl.Append($"{episode.number} серия", title ?? original_title, s.ToString(), episode.number.ToString(), streams[0].link, streamquality: new StreamQualityTpl(streams), subtitles: subtitles, vast: vast);
                        }
                        #endregion

                        if (rjson)
                            return etpl.ToJson(vtpl);

                        return vtpl.ToHtml() + etpl.ToHtml();
                    }
                    else
                    {
                        var etpl = new EpisodeTpl();

                        foreach (var episode in root.item.seasons.First(i => i.number == s).episodes)
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

                                etpl.Append($"{episode.number} серия", title ?? original_title, s.ToString(), episode.number.ToString(), onstreamfile(episode.files[0].url.hls4, null), voice_name: voicename, vast: vast);
                            }
                            else
                            {
                                if (episode.files[0].url.http == null)
                                    continue;

                                #region subtitle
                                var subtitles = new SubtitleTpl();

                                if (episode.subtitles != null)
                                {
                                    foreach (var sub in episode.subtitles)
                                    {
                                        if (sub.url != null)
                                            subtitles.Append(sub.lang, onstreamfile(sub.url, null));
                                    }
                                }
                                #endregion

                                #region streams
                                var streams = new List<(string link, string quality)>() { Capacity = episode.files.Count };

                                foreach (var f in episode.files)
                                {
                                    if (f.url.http != null)
                                        streams.Add((onstreamfile(f.url.http, f.file), f.quality));
                                }
                                #endregion

                                string mp4 = onstreamfile(episode.files[0].url.http, episode.files[0].file);
                                etpl.Append($"{episode.number} серия", title ?? original_title, s.ToString(), episode.number.ToString(), mp4, subtitles: subtitles, voice_name: voicename, streamquality: new StreamQualityTpl(streams), vast: vast);
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

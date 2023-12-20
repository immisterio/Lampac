using Lampac.Models.LITE.KinoPub;
using Shared.Model.Online.KinoPub;
using Shared.Model.Templates;
using System.Text;
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
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public KinoPubInvoke(string? host, string apihost, string? token, Func<string, ValueTask<string?>> onget, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.token = token;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
        }
        #endregion

        #region Search
        /// <returns>
        /// 0 - Данные не получены
        /// 1 - Нет нужного контента 
        /// </returns>
        async public ValueTask<(int id, string? similars)> Search(string? title, string? original_title, int year, int clarification, string? imdb_id, long kinopoisk_id)
        {
            string? searchtitle = clarification == 1 ? title : (original_title ?? title);
            if (string.IsNullOrWhiteSpace(searchtitle))
                return (0, null);

            string? json = await onget($"{apihost}/v1/items/search?q={HttpUtility.UrlEncode(searchtitle)}&access_token={token}&field=title&perpage=200");
            if (json == null)
                return (0, null);

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
                            if (item.year == year || (item.year == year - 1) || (item.year == year + 1))
                            {
                                stpl.Append(item.title, item.year.ToString(), item.voice, host + $"lite/kinopub?postid={item.id}&title={enc_title}&original_title={enc_original_title}");

                                string _t = item.title.ToLower();
                                string _s = searchtitle.ToLower();

                                if (item.year == year && (_t.StartsWith(_s) || _t.EndsWith(_s)))
                                    ids.Add(item.id);
                            }
                        }

                        if (ids.Count == 1)
                            return (ids[0], null);

                        return (0, stpl.ToHtml());
                    }
                    else
                    {
                        foreach (var item in items)
                        {
                            if (item.kinopoisk > 0 && item.kinopoisk == kinopoisk_id)
                                return (item.id, null);

                            if ($"tt{item.imdb}" == imdb_id)
                                return (item.id, null);
                        }

                        return (-1, null);
                    }
                }
            }
            catch { }

            return (0, null);
        }
        #endregion

        #region Post
        async public ValueTask<RootObject?> Post(int postid)
        {
            string? json = await onget($"{apihost}/v1/items/{postid}?access_token={token}");
            if (json == null)
                return null;

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
        public string Html(RootObject root, string? filetype, string? title, string? original_title, int postid, int s = -1)
        {
            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            if (root?.item?.videos != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, root.item.videos.Count);

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
                        mtpl.Append(v.files[0].quality, onstreamfile(v.files[0].url.hls4), voice_name: voicename);
                    }
                    else
                    {
                        #region subtitle
                        var subtitles = new SubtitleTpl();

                        if (v.subtitles != null)
                        {
                            foreach (var sub in v.subtitles)
                                subtitles.Append(sub.lang, onstreamfile(sub.url));
                        }
                        #endregion

                        var streamquality = new StreamQualityTpl(v.files.Select(f => (onstreamfile(f.url.http), f.quality)));
                        mtpl.Append(v.files[0].quality, onstreamfile(v.files[0].url.http), subtitles: subtitles, voice_name: voicename, streamquality: streamquality);
                    }
                }

                return mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                string? enc_title = HttpUtility.UrlEncode(title);
                string? enc_original_title = HttpUtility.UrlEncode(original_title);

                if (s == -1)
                {
                    #region Сезоны
                    foreach (var season in root.item.seasons)
                    {
                        string link = host + $"lite/kinopub?postid={postid}&title={enc_title}&original_title={enc_original_title}&s={season.number}";

                        html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season.number} сезон" + "</div></div></div>");
                        firstjson = false;
                    }
                    #endregion
                }
                else
                {
                    #region Серии
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
                            html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.number + "\" data-json='{\"method\":\"play\",\"url\":\"" + onstreamfile(episode.files[0].url.hls4) + "\",\"title\":\"" + $"{title ?? original_title} ({episode.number} серия)" + "\", \"voice_name\":\"" + voicename + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.number} серия" + "</div></div>");
                            firstjson = false;
                        }
                        else
                        {
                            #region subtitle
                            var subtitles = new SubtitleTpl();

                            if (episode.subtitles != null)
                            {
                                foreach (var sub in episode.subtitles)
                                    subtitles.Append(sub.lang, onstreamfile(sub.url));
                            }
                            #endregion

                            #region streansquality
                            string streansquality = string.Empty;

                            foreach (var f in episode.files)
                            {
                                string l = onstreamfile(f.url.http);
                                streansquality += $"\"{f.quality}\":\"" + l + "\",";
                            }

                            streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";
                            #endregion

                            string mp4 = onstreamfile(episode.files[0].url.http);

                            html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.number + "\" data-json='{\"method\":\"play\",\"url\":\"" + mp4 + "\",\"title\":\"" + $"{title ?? original_title} ({episode.number} серия)" + "\", \"subtitles\": [" + subtitles.ToHtml() + "], \"voice_name\":\"" + voicename + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.number} серия" + "</div></div>");
                            firstjson = false;
                        }
                    }
                    #endregion
                }
                #endregion
            }

            return html.ToString() + "</div>";
        }
        #endregion
    }
}

﻿using Lampac.Models.LITE.Collaps;
using Shared.Model.Online;
using Shared.Model.Online.Collaps;
using Shared.Model.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class CollapsInvoke
    {
        #region CollapsInvoke
        string? host;
        string apihost;
        bool dash;
        Func<string, ValueTask<string?>> onget;
        Func<string, string> onstreamfile;
        Action? requesterror;

        public CollapsInvoke(string? host, string apihost, bool dash, Func<string, ValueTask<string?>> onget, Func<string, string> onstreamfile, Action? requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.dash = dash;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        public async ValueTask<EmbedModel?> Embed(string? imdb_id, long kinopoisk_id)
        {
            string uri = $"{apihost}/embed/imdb/{imdb_id}";
            if (kinopoisk_id > 0)
                uri = $"{apihost}/embed/kp/{kinopoisk_id}";

            string? content = await onget.Invoke(uri);
            if (string.IsNullOrEmpty(content))
            {
                requesterror?.Invoke();
                return null;
            }

            if (!content.Contains("seasons:"))
                return new EmbedModel() { content = content };

            List<RootObject>? root = null;

            try
            {
                root = JsonSerializer.Deserialize<List<RootObject>>(Regex.Match(content, "seasons:([^\n\r]+)").Groups[1].Value);
                if (root == null || root.Count == 0)
                    return null;
            }
            catch { return null; }

            return new EmbedModel() { serial = root };
        }
        #endregion

        #region Html
        public string Html(EmbedModel? md, string? imdb_id, long kinopoisk_id, string? title, string? original_title, int s, bool rjson = false, List<HeadersModel>? headers = null)
        {
            if (md == null)
                return string.Empty;

            if (md.content != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);

                string stream = Regex.Match(md.content, "hls: +\"(https?://[^\"]+\\.m3u[^\"]+)\"").Groups[1].Value;

                if (dash)
                {
                    string _dash = Regex.Match(md.content, "dash: +\"(https?://[^\"]+\\.mp[^\"]+)\"").Groups[1].Value;
                    if (!string.IsNullOrEmpty(_dash))
                        stream = _dash;
                }

                if (string.IsNullOrEmpty(stream))
                    return string.Empty;

                string name = Regex.Match(md.content, "audio: +\\{\"names\":\\[\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(name))
                    name = "По умолчанию";

                #region subtitle
                var subtitles = new SubtitleTpl();

                try
                {
                    var subs = JsonSerializer.Deserialize<List<Cc>>(Regex.Match(md.content, "cc: +(\\[[^\n\r]+\\]),").Groups[1].Value);
                    if (subs != null)
                    {
                        foreach (var cc in subs) 
                            subtitles.Append(cc.name, onstreamfile.Invoke(cc.url));
                    }
                }
                catch { }
                #endregion

                string voicename = Regex.Match(md.content, "audio: +\\{\"names\":\\[\"([^\\]]+)\\]").Groups[1].Value;
                voicename = voicename.Replace("\"", "").Replace("delete", "").Replace(",", ", ");
                voicename = Regex.Replace(voicename, "[, ]+$", "");

                mtpl.Append(name, onstreamfile.Invoke(stream.Replace("\u0026", "&")), subtitles: subtitles, voice_name: voicename, headers: headers);

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
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
                        var tpl = new SeasonTpl(md.serial.Count);

                        foreach (var season in md.serial.OrderBy(i => i.season))
                        {
                            string link = host + $"lite/collaps?rjson={rjson}&kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&title={enc_title}&original_title={enc_original_title}&s={season.season}";
                            tpl.Append($"{season.season} сезон", link, season.season);
                        }

                        return rjson ? tpl.ToJson() : tpl.ToHtml();
                    }
                    else
                    {
                        var episodes = md.serial.First(i => i.season == s).episodes;
                        if (episodes == null)
                            return string.Empty;

                        var etpl = new EpisodeTpl();

                        foreach (var episode in episodes)
                        {
                            string? stream = episode?.hls ?? episode?.dash;
                            if (dash && episode?.dash != null)
                                stream = episode.dash;

                            if (string.IsNullOrEmpty(stream) || string.IsNullOrEmpty(episode?.episode))
                                continue;

                            #region voicename
                            string voicename = string.Empty;

                            if (episode?.audio?.names != null)
                                voicename = Regex.Replace(string.Join(", ", episode.audio.names), "[, ]+$", "");
                            #endregion

                            #region subtitle
                            var subtitles = new SubtitleTpl();

                            if (episode?.cc != null && episode.cc.Count > 0)
                            {
                                foreach (var cc in episode.cc)
                                    subtitles.Append(cc.name, onstreamfile.Invoke(cc.url));
                            }
                            #endregion

                            string file = onstreamfile.Invoke(stream.Replace("\u0026", "&"));
                            etpl.Append($"{episode.episode} серия", title ?? original_title, s.ToString(), episode.episode, file, subtitles: subtitles, voice_name: voicename, headers: headers);
                        }

                        return rjson ? etpl.ToJson() : etpl.ToHtml();
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

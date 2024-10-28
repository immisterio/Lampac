﻿using Shared.Model.Online;
using Shared.Model.Online.VideoDB;
using Shared.Model.Templates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class VideoDBInvoke
    {
        #region VideoDBInvoke
        string? host;
        string apihost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;
        Func<string, List<HeadersModel>?, ValueTask<string?>> onget;

        public VideoDBInvoke(string? host, string? apihost, Func<string, List<HeadersModel>?, ValueTask<string?>> onget, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost!;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.onget = onget;
        }
        #endregion

        #region Embed
        public async ValueTask<EmbedModel?> Embed(long kinopoisk_id)
        {
            string? html = await onget.Invoke($"{apihost}/embed/AN?kinopoisk_id={kinopoisk_id}", null);

            if (html == null)
            {
                onlog?.Invoke("html null");
                return null;
            }

            return Embed(html);
        }

        public EmbedModel? Embed(string html)
        {
            if (string.IsNullOrEmpty(html))
                return null;

            string? decodePlayer()
            {
                try
                {
                    string base64 = Regex.Match(html, "new Player\\(\"([^\n\r]+)\"\\);").Groups[1].Value.Remove(0, 3);
                    base64 = Regex.Replace(base64, "//[^=]+=", "");
                    string json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                    //json = json.Split("\"player\",\"file\":")[1].Split(",\"hls\":")[0];

                    return json;
                }
                catch 
                {
                    return null;
                }
            }

            string? file = decodePlayer();
            if (file == null)
                return null;

            List<RootObject>? pl = JsonNode.Parse(file)?["file"]?.Deserialize<List<RootObject>>();
            if (pl == null || pl.Count == 0) 
                return null;

            string quality = file.Contains("1080p") ? "1080p" : file.Contains("720p") ? "720p" : "480p";
            return new EmbedModel() { pl = pl, movie = !file.Contains("\"folder\":"), quality = quality };
        }
        #endregion

        #region Html
        public string Html(EmbedModel? root, long kinopoisk_id, string? title, string? original_title, string? t, int s, int sid, bool rjson)
        {
            if (root?.pl == null || root.pl.Count == 0)
                return string.Empty;

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            if (root.movie)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, root.pl.Count);

                foreach (var pl in root.pl)
                {
                    string? name = pl.title;
                    string? file = pl.file;

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                        continue;

                    #region streams
                    var streams = new List<(string link, string quality)>() { Capacity = 4 };

                    foreach (Match m in Regex.Matches(file, $"\\[(1080|720|480|360)p?\\]([^\"\\,\\[ ]+)"))
                    {
                        string link = m.Groups[2].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        streams.Insert(0, (host + $"lite/videodb/manifest.m3u8?link={HttpUtility.UrlEncode(link)}&title={enc_title}&original_title={enc_original_title}", $"{m.Groups[1].Value}p"));
                    }

                    if (streams.Count == 0)
                        continue;
                    #endregion

                    #region subtitle (off)
                    var subtitles = new SubtitleTpl();

                    //try
                    //{
                    //    int subx = 1;
                    //    var subs = pl.subtitle;
                    //    if (subs != null)
                    //    {
                    //        foreach (string cc in subs.Split(","))
                    //        {
                    //            if (string.IsNullOrWhiteSpace(cc) || !cc.EndsWith(".srt"))
                    //                continue;

                    //            subtitles.Append($"sub #{subx}", onstreamfile.Invoke(cc));
                    //            subx++;
                    //        }
                    //    }
                    //}
                    //catch { }
                    #endregion

                    mtpl.Append(name, streams[0].link, "call", $"{streams[0].link}&play=true", subtitles: subtitles, streamquality: new StreamQualityTpl(streams));
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                if (s == -1)
                {
                    var tpl = new SeasonTpl(root.quality);

                    for(int i = 0; i < root.pl.Count; i++)
                    {
                        string? name = root.pl?[i].title;
                        if (name == null)
                            continue;

                        string season = Regex.Match(name, "^([0-9]+)").Groups[1].Value;
                        if (string.IsNullOrEmpty(season))
                            continue;

                        tpl.Append(name, host + $"lite/videodb?kinopoisk_id={kinopoisk_id}&rjson={rjson}&title={enc_title}&original_title={enc_original_title}&s={season}&sid={i}");
                    }

                    return tpl.ToHtml();
                }
                else
                {
                    var season = root.pl?[sid]?.folder;
                    if (season == null)
                        return string.Empty;

                    var hashvoices = new HashSet<string>();
                    var htmlvoices = new StringBuilder();
                    var htmlepisodes = new StringBuilder();

                    foreach (var episode in season)
                    {
                        var episodes = episode?.folder;
                        if (episodes == null || episodes.Count == 0)
                            continue;

                        foreach (var pl in episodes)
                        {
                            // MVO | LostFilm
                            string perevod = Regex.Replace(pl?.title ?? "", "^[a-zA-Z]{3} \\| ", "");
                            if (!string.IsNullOrEmpty(perevod) && string.IsNullOrEmpty(t))
                                t = perevod;

                            #region Переводы
                            if (!hashvoices.Contains(perevod))
                            {
                                hashvoices.Add(perevod);
                                string link = host + $"lite/videodb?kinopoisk_id={kinopoisk_id}&rjson={rjson}&title={enc_title}&original_title={enc_original_title}&s={s}&sid={sid}&t={HttpUtility.UrlEncode(perevod)}";
                                string active = t == perevod ? "active" : "";

                                htmlvoices.Append("<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + perevod + "</div>");
                            }
                            #endregion

                            if (perevod != t)
                                continue;

                            // 1 эпизод 
                            string? name = episode?.title;
                            string? file = pl?.file;

                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                                continue;

                            var streams = new List<(string link, string quality)>() { Capacity = 4 };

                            foreach (Match m in Regex.Matches(file, $"\\[(1080|720|480|360)p?\\]([^\"\\,\\[ ]+)"))
                            {
                                string link = m.Groups[2].Value;
                                if (string.IsNullOrEmpty(link))
                                    continue;

                                streams.Insert(0, (host + $"lite/videodb/manifest.m3u8?link={HttpUtility.UrlEncode(link)}&title={enc_title}&original_title={enc_original_title}", $"{m.Groups[1].Value}p"));
                            }

                            if (streams.Count == 0)
                                continue;

                            string streansquality = "\"quality\": {" + string.Join(",", streams.Select(s => $"\"{s.quality}\":\"{s.link}\"")) + "}";

                            string streamlink = ",\"stream\":\"" + $"{streams[0].link}&play=true" + "\"";

                            htmlepisodes.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(name, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"call\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + $"{title ?? original_title} ({name})" + "\", " + streansquality + streamlink + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + name + "</div></div>");
                            firstjson = false;
                        }
                    }

                    html.Append(htmlvoices.ToString() + "</div><div class=\"videos__line\">" + htmlepisodes.ToString());
                }
                #endregion
            }

            return html.ToString() + "</div>";
        }
        #endregion
    }
}

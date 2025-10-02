using Shared.Models;
using Shared.Models.Online.VideoDB;
using Shared.Models.Templates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public struct VideoDBInvoke
    {
        #region VideoDBInvoke
        string host;
        string apihost;
        Func<string, string> onstreamfile;
        Func<string, string> onlog;
        Func<string, List<HeadersModel>, ValueTask<string>> onget;

        public VideoDBInvoke(string host, string apihost, Func<string, List<HeadersModel>, ValueTask<string>> onget, Func<string, string> onstreamfile, Func<string, string> onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost!;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.onget = onget;
        }
        #endregion

        #region Embed
        public async ValueTask<EmbedModel> Embed(long kinopoisk_id)
        {
            string html = await onget.Invoke($"{apihost}/embed/AN?kinopoisk_id={kinopoisk_id}", null);

            if (html == null)
            {
                onlog?.Invoke("VideoDB: html null");
                return null;
            }

            return Embed(html);
        }

        public EmbedModel Embed(in string html)
        {
            if (string.IsNullOrEmpty(html))
                return null;

            string decodePlayer(in string _html)
            {
                try
                {
                    string base64 = Regex.Match(_html, "new Player\\(\"([^\n\r]+)\"\\);").Groups[1].Value.Remove(0, 73);
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

            string file = decodePlayer(html);
            if (file == null)
            {
                onlog?.Invoke("VideoDB: file null");
                return null;
            }

            onlog?.Invoke("VideoDB: file OK");

            var pl = JsonNode.Parse(file)?["file"]?.Deserialize<RootObject[]>();
            if (pl == null || pl.Length == 0)
            {
                onlog?.Invoke("VideoDB: pl null");
                return null;
            }

            onlog?.Invoke("VideoDB: pl OK");

            string quality = file.Contains("2160p") ? "2160p" : file.Contains("1080p") ? "1080p" : file.Contains("720p") ? "720p" : "480p";
            return new EmbedModel() { pl = pl, movie = !file.Contains("\"folder\":"), quality = quality };
        }
        #endregion

        #region Html
        public string Html(EmbedModel root, string args, long kinopoisk_id, string title, string original_title, string t, int s, int sid, bool rjson, bool bwa = false, bool rhub = false)
        {
            if (root?.pl == null || root.pl.Length == 0)
                return string.Empty;

            if (!string.IsNullOrEmpty(args))
                args = $"&{args.Remove(0, 1)}";

            string enc_title = HttpUtility.UrlEncode(title);
            string enc_original_title = HttpUtility.UrlEncode(original_title);

            if (root.movie)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, root.pl.Length);

                foreach (var pl in root.pl)
                {
                    string name = pl.title;
                    string file = pl.file;

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                        continue;

                    #region streams
                    var streams = new List<(string link, string quality)>(7);

                    foreach (Match m in Regex.Matches(file, $"\\[(Авто|2160|1440|1080|720|480|360)p?\\]([^\"\\,\\[ ]+)"))
                    {
                        string link = m.Groups[2].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        string quality = file.Contains("2160p") ? "2160" : file.Contains("1080p") ? "1080" : file.Contains("720p") ? "720" : "480";
                        streams.Add((host + $"lite/videodb/manifest.m3u8?link={HttpUtility.UrlEncode(link)}{args}", quality));
                    }

                    if (streams.Count == 0)
                        continue;

                    streams.Reverse();
                    #endregion

                    #region subtitle (off)
                    //var subtitles = new SubtitleTpl();

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

                    if (bwa || rhub)
                    {
                        mtpl.Append(name, streams[0].link.Replace("/manifest.m3u8", "/manifest"), "call");
                    }
                    else
                    {
                        mtpl.Append(name, streams[0].link, quality: streams[0].quality);
                    }
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                if (s == -1)
                {
                    var tpl = new SeasonTpl(root.quality, root.pl.Length);

                    for(int i = 0; i < root.pl.Length; i++)
                    {
                        string name = root.pl?[i].title;
                        if (name == null)
                            continue;

                        string season = Regex.Match(name, "^([0-9]+)").Groups[1].Value;
                        if (string.IsNullOrEmpty(season))
                            continue;

                        tpl.Append(name, host + $"lite/videodb?rjson={rjson}&kinopoisk_id={kinopoisk_id}&rjson={rjson}&title={enc_title}&original_title={enc_original_title}&s={season}&sid={i}", season);
                    }

                    return rjson ? tpl.ToJson() : tpl.ToHtml();
                }
                else
                {
                    var season = root.pl[sid].folder;
                    if (season == null)
                        return string.Empty;

                    var vtpl = new VoiceTpl();
                    var etpl = new EpisodeTpl();

                    var hashvoices = new HashSet<string>();

                    string sArhc = s.ToString();

                    foreach (var episode in season)
                    {
                        var episodes = episode.folder;
                        if (episodes == null || episodes.Length == 0)
                            continue;

                        foreach (var pl in episodes)
                        {
                            // MVO | LostFilm
                            string perevod = Regex.Replace(pl.title ?? "", "^[a-zA-Z]{3} \\| ", "");
                            if (!string.IsNullOrEmpty(perevod) && string.IsNullOrEmpty(t))
                                t = perevod;

                            #region Переводы
                            if (!hashvoices.Contains(perevod))
                            {
                                hashvoices.Add(perevod);
                                string link = host + $"lite/videodb?rjson={rjson}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&s={s}&sid={sid}&t={HttpUtility.UrlEncode(perevod)}";

                                vtpl.Append(perevod, t == perevod, link);
                            }
                            #endregion

                            if (perevod != t)
                                continue;

                            // 1 эпизод 
                            string name = episode.title;
                            string file = pl.file;

                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                                continue;

                            var streamquality = new StreamQualityTpl();

                            foreach (Match m in Regex.Matches(file, $"\\[(1080|720|480|360)p?\\]([^\"\\,\\[ ]+)"))
                            {
                                string link = m.Groups[2].Value;
                                if (string.IsNullOrEmpty(link))
                                    continue;

                                streamquality.Insert(host + $"lite/videodb/manifest.m3u8?serial=true&link={HttpUtility.UrlEncode(link)}{args}", $"{m.Groups[1].Value}p");
                            }

                            if (!streamquality.Any())
                                continue;

                            if (bwa || rhub)
                            {
                                string streamlink = rhub ? streamquality.Firts().link : null;
                                etpl.Append(name, title ?? original_title, sArhc, Regex.Match(name, "^([0-9]+)").Groups[1].Value, streamquality.Firts().link.Replace("/manifest.m3u8", "/manifest"), "call", streamlink: streamlink);
                            }
                            else
                            {
                                etpl.Append(name, title ?? original_title, sArhc, Regex.Match(name, "^([0-9]+)").Groups[1].Value, streamquality.Firts().link, streamquality: streamquality);
                            }
                        }
                    }

                    if (rjson)
                        return etpl.ToJson(vtpl);

                    return vtpl.ToHtml() + etpl.ToHtml();
                }
                #endregion
            }
        }
        #endregion
    }
}

using Shared.Models.Base;
using Shared.Models.Online.Kinobase;
using Shared.Models.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public struct KinobaseInvoke
    {
        #region KinobaseInvoke
        KinobaseSettings init;
        string host;
        string apihost;
        Func<string, ValueTask<string>> onget;
        Func<string, string> onstreamfile;
        Func<string, string> onlog;
        Action requesterror;

        public KinobaseInvoke(string host, KinobaseSettings init, Func<string, ValueTask<string>> onget, Func<string, string> onstreamfile, Func<string, string> onlog = null, Action requesterror = null)
        {
            this.init = init;
            this.host = host != null ? $"{host}/" : null;
            apihost = init.host;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.requesterror = requesterror;
        }
        #endregion

        #region Search
        async public Task<SearchModel> Search(string title, int year)
        {
            if (string.IsNullOrEmpty(title))
                return null;

            string content = await onget($"{apihost}/search?query={HttpUtility.UrlEncode(title)}");
            if (content == null)
            {
                requesterror?.Invoke();
                return null;
            }

            var rows = content.Split("<li class=\"item\">");
            string link = null;

            var similar = new SimilarTpl(rows.Length);

            foreach (string row in rows.Skip(1))
            {
                if (row.Contains(">Трейлер</span>"))
                    continue;

                string name = Regex.Match(row, "<div class=\"title\"><[^>]+>([^<]+)").Groups[1].Value;
                string _year = Regex.Match(row, "<span class=\"year\">([0-9]+)").Groups[1].Value;
                string img = Regex.Match(row, "<img src=\"/([^\"]+)\"").Groups[1].Value;
                if (!string.IsNullOrEmpty(img))
                    img = $"{apihost}/{img}";

                string rlnk = Regex.Match(row, "href=\"/([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(rlnk) || string.IsNullOrEmpty(name))
                    continue;

                string uri = host + $"lite/kinobase?href={HttpUtility.UrlEncode(rlnk)}";
                similar.Append(name, _year, string.Empty, uri, PosterApi.Size(img));

                if (StringConvert.SearchName(name) == StringConvert.SearchName(title) && _year == year.ToString())
                {
                    if (string.IsNullOrEmpty(link))
                        link = rlnk;
                }
            }

            if (string.IsNullOrEmpty(link))
            {
                if (!content.Contains(">По запросу") && similar.data.Count == 0)
                {
                    requesterror?.Invoke();
                    return null;
                }
            }

            return new SearchModel() 
            {
                link = link,
                similar = similar
            };
        }
        #endregion

        #region Embed
        async public Task<EmbedModel> Embed(string link)
        {
            if (string.IsNullOrEmpty(link))
                return null;

            string news = await onget($"{apihost}/{link}");
            if (string.IsNullOrEmpty(news))
            {
                requesterror?.Invoke();
                return null;
            }

            if (news.Contains("id=\"playerjsfile\""))
            {
                try
                {
                    string video = Regex.Match(news, "id=\"playerjsfile\">([^<]+)<").Groups[1].Value;
                    if (string.IsNullOrEmpty(video))
                    {
                        requesterror?.Invoke();
                        return null;
                    }

                    if (video.EndsWith("]"))
                    {
                        var res = JsonSerializer.Deserialize<Season[]>(video);
                        if (res == null || res.Length == 0)
                            return null;

                        return new EmbedModel()
                        {
                            serial = res,
                            quality = (video.Contains("2160.") || video.Contains("2160_")) ? "2160p" : (video.Contains("1440.") || video.Contains("1440_")) ? "1440p" : (video.Contains("1080.") || video.Contains("1080_")) ? "1080p" : video.Contains("720.") ? "720p" : video.Contains("480.") ? "480p" : "360p"
                        };
                    }
                    else
                    {
                        return new EmbedModel() { content = video };
                    }
                }
                catch { return null; }
            }
            else
            {
                #region uppod.js
                if (news.Contains("id=\"playlists\""))
                {
                    try
                    {
                        string video = Regex.Match(news, "id=\"playlists\">([^<]+)<").Groups[1].Value;
                        if (string.IsNullOrEmpty(video))
                        {
                            requesterror?.Invoke();
                            return null;
                        }

                        var res = JsonSerializer.Deserialize<Season[]>(video);
                        if (res == null || res.Length == 0)
                            return null;

                        return new EmbedModel()
                        {
                            serial = res,
                            quality = video.Contains("1080.") ? "1080p" : video.Contains("720.") ? "720p" : video.Contains("480.") ? "480p" : "360p"
                        };
                    }
                    catch { return null; }
                }
                else
                {
                    string video = StringConvert.FindLastText(news, "id=\"videoplayer\"", "</div>");
                    if (string.IsNullOrEmpty(video))
                    {
                        requesterror?.Invoke();
                        return null;
                    }

                    return new EmbedModel() { content = video };
                }
                #endregion
            }
        }
        #endregion

        #region Html
        public string Html(EmbedModel md, string title, string href, int s, string t, bool rjson = false)
        {
            if (md == null || md.IsEmpty)
                return string.Empty;

            if (md.content != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title);

                if (init.playerjs)
                {
                    var voices = md.content.Split("{");
                    var hash = new HashSet<string>();

                    foreach (string line in voices)
                    {
                        string voice = Regex.Match(line, "([^\\}]+)\\}").Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(voice) && !hash.Contains(voice))
                        {
                            hash.Add(voice);

                            var streams = new List<(string link, string quality)>(6);

                            foreach (string q in new string[] { "2160", "1440", "1080", "720", "480", "360" })
                            {
                                foreach (var line2 in voices)
                                {
                                    if (line2.Contains(voice) && (line2.Contains($"_{q}") || line2.Contains($"_{q}")))
                                    {
                                        string links = Regex.Match(line2, "\\}([^\\[,;]+)").Groups[1].Value;
                                        if (string.IsNullOrEmpty(links))
                                            continue;

                                        streams.Add((onstreamfile.Invoke(links), $"{q}p"));
                                    }
                                }
                            }

                            if (streams.Count > 0)
                                mtpl.Append(voice, streams[0].link, streamquality: new StreamQualityTpl(streams));
                        }
                    }
                }
                else
                {
                    var streams = new List<(string link, string quality)>(4);

                    foreach (string q in new string[] { "1080", "720", "480", "360" })
                    {
                        string link = Regex.Match(md.content, $"(https?://[^\"\\[\\|,;\n\r\t ]+_{q}(_10)?.(mp4|m3u8))").Groups[1].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        streams.Add((onstreamfile.Invoke(link), $"{q}p"));
                    }

                    if (streams.Count == 0)
                        return string.Empty;

                    mtpl.Append(streams[0].quality, streams[0].link, streamquality: new StreamQualityTpl(streams));
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                string enc_title = HttpUtility.UrlEncode(title);
                string sArhc = s.ToString();

                if (init.playerjs)
                {
                    if (md.serial.First().folder == null)
                    {
                        if (s == -1)
                        {
                            var tpl = new SeasonTpl(md.quality);
                            string link = host + $"lite/kinobase?title={enc_title}&href={HttpUtility.UrlEncode(href)}&t={HttpUtility.UrlEncode(t)}&s=1";
                            tpl.Append("1 сезон", link, 1);

                            return rjson ? tpl.ToJson() : tpl.ToHtml();
                        }
                        else
                        {
                            return renderSeason(md.serial, host, onstreamfile);
                        }
                    }
                    else
                    {
                        if (s == -1)
                        {
                            var tpl = new SeasonTpl(md.quality);

                            foreach (var item in md.serial)
                            {
                                string season = Regex.Match(item.title.Trim(), "^([0-9]+)").Groups[1].Value;
                                string link = host + $"lite/kinobase?title={enc_title}&href={HttpUtility.UrlEncode(href)}&t={HttpUtility.UrlEncode(t)}&s={season}";
                                tpl.Append($"{season} сезон", link, season);
                            }

                            return rjson ? tpl.ToJson() : tpl.ToHtml();
                        }
                        else
                        {
                            return renderSeason(md.serial.First(i => i.title.StartsWith($"{s} ")).folder, host, onstreamfile);
                        }
                    }

                    string renderSeason(Season[] episodes, string host, Func<string, string> onstreamfile)
                    {
                        #region Перевод
                        var vtpl = new VoiceTpl();

                        {
                            var hash_voices = new HashSet<string>();
                            var m = Regex.Match(episodes.First().file, "\\{([^\\}]+)");
                            while (m.Success)
                            {
                                string voice = m.Groups[1].Value.Trim();
                                if (!hash_voices.Contains(voice))
                                {
                                    hash_voices.Add(voice);

                                    if (string.IsNullOrEmpty(t))
                                        t = voice;

                                    string link = host + $"lite/kinobase?title={enc_title}&href={HttpUtility.UrlEncode(href)}&t={HttpUtility.UrlEncode(voice)}&s={s}";
                                    bool active = t == voice;

                                    vtpl.Append(voice, active, link);
                                }

                                m = m.NextMatch();
                            }
                        }
                        #endregion

                        var etpl = new EpisodeTpl(episodes.Length);

                        foreach (var episode in episodes)
                        {
                            if (string.IsNullOrEmpty(episode.file) || (t != null && !episode.file.Contains(t)))
                                continue;

                            var streams = new List<(string link, string quality)>(6);

                            foreach (string quality in new List<string> { "2160", "1440", "1080", "720", "480", "360" })
                            {
                                string qline = Regex.Match(episode.file, $"\\[{quality}p( [^\\]]+)?\\]([^\\[]+)").Groups[2].Value;
                                if (string.IsNullOrEmpty(qline))
                                    continue;

                                if (string.IsNullOrEmpty(t))
                                {
                                    string links = Regex.Match(qline, "({[^\\}]+})?([^\\}\\{\\[,;]+)").Groups[2].Value;
                                    if (string.IsNullOrEmpty(links))
                                        continue;

                                    streams.Add((onstreamfile.Invoke(links), $"{quality}p"));
                                }
                                else
                                {
                                    string links = Regex.Match(qline, "{" + Regex.Escape(t) + "}" + "([^,;]+)").Groups[1].Value;
                                    if (string.IsNullOrEmpty(links))
                                        continue;

                                    streams.Add((onstreamfile.Invoke(links), $"{quality}p"));
                                }
                            }

                            if (streams.Count == 0)
                                continue;

                            #region subtitle
                            var subtitles = new SubtitleTpl();

                            if (!string.IsNullOrEmpty(episode.subtitle))
                            {
                                var m = Regex.Match(episode.subtitle, "\\[([^\\]]+)\\]([^\t ]+)");
                                while (m.Success)
                                {
                                    subtitles.Append(m.Groups[1].Value, onstreamfile.Invoke(m.Groups[2].Value));

                                    m = m.NextMatch();
                                }
                            }
                            #endregion


                            etpl.Append(episode.title, title, sArhc, Regex.Match(episode.title, "^([0-9]+)").Groups[1].Value, streams[0].link, subtitles: subtitles, streamquality: new StreamQualityTpl(streams));
                        }

                        if (rjson)
                            return etpl.ToJson(vtpl);

                        return vtpl.ToHtml() + etpl.ToHtml();
                    }
                }
                else
                {
                    #region uppod.js
                    string finEpisode(Season[] data, Func<string, string> onstreamfile)
                    {
                        var etpl = new EpisodeTpl(data.Length);

                        foreach (var episode in data)
                        {
                            if (string.IsNullOrEmpty(episode.file))
                                continue;

                            var streams = new List<(string link, string quality)>(4);

                            foreach (string quality in new List<string> { "1080", "720", "480", "360" })
                            {
                                string link = Regex.Match(episode.file, $"(https?://[^\"\\[\\|,;\n\r\t ]+_{quality}.(mp4|m3u8))").Groups[1].Value;
                                if (string.IsNullOrEmpty(link))
                                    continue;

                                streams.Add((onstreamfile.Invoke(link), $"{quality}p"));
                            }

                            if (streams.Count == 0)
                                continue;

                            etpl.Append(episode.title, title, sArhc, Regex.Match(episode.title, "^([0-9]+)").Groups[1].Value, streams[0].link, streamquality: new StreamQualityTpl(streams));
                        }

                        return rjson ? etpl.ToJson() : etpl.ToHtml();
                    }

                    if (md.serial.First().folder == null)
                    {
                        if (s == -1)
                        {
                            var tpl = new SeasonTpl(md.quality);
                            string link = host + $"lite/kinobase?title={enc_title}&href={HttpUtility.UrlEncode(href)}&s=1";
                            tpl.Append("1 сезон", link, 1);

                            return rjson ? tpl.ToJson() : tpl.ToHtml();
                        }
                        else
                        {
                            return finEpisode(md.serial, onstreamfile);
                        }
                    }
                    else
                    {
                        if (s == -1)
                        {
                            var tpl = new SeasonTpl(md.quality);

                            foreach (var item in md.serial)
                            {
                                string season = Regex.Match(item.title.Trim(), "^([0-9]+)").Groups[1].Value;
                                string link = host + $"lite/kinobase?title={enc_title}&href={HttpUtility.UrlEncode(href)}&s={season}";
                                tpl.Append($"{season} сезон", link, season);
                            }

                            return rjson ? tpl.ToJson() : tpl.ToHtml();
                        }
                        else
                        {
                            return finEpisode(md.serial.First(i => i.title.StartsWith($"{s} ")).folder, onstreamfile);
                        }
                    }
                    #endregion
                }
                #endregion
            }
        }
        #endregion
    }
}

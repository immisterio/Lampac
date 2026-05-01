using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.Services.RxEnumerate;
using Shared.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Kinobase;

public struct KinobaseInvoke
{
    #region KinobaseInvoke
    ModuleConf init;
    string host;
    string apihost;
    Func<string, Task<string>> onget;
    Func<string, string> onstreamfile;

    public KinobaseInvoke(string host, ModuleConf init, Func<string, Task<string>> onget, Func<string, string> onstreamfile)
    {
        this.init = init;
        this.host = host != null ? $"{host}/" : null;
        apihost = init.host;
        this.onget = onget;
        this.onstreamfile = onstreamfile;
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
            return null;
        }

        string link = null;

        var rx = Rx.Split("<li class=\"x1 item\">", content, 1);

        var similar = new SimilarTpl(rx.Count);

        foreach (var row in rx.Rows())
        {
            if (row.Contains(">Трейлер</span>"))
                continue;

            string name = row.Match("<div class=\"title\"><[^>]+>([^<]+)");
            string _year = row.Match("<span class=\"year\">([0-9]+)");
            string img = row.Match("<img (data-src|src)=\"/([^\"]+)\"", 2);
            if (!string.IsNullOrEmpty(img))
                img = $"{apihost}/{img}";

            string rlnk = row.Match("href=\"/([^\"]+)\"");
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
            if (!content.Contains(">По запросу") && similar.IsEmpty)
            {
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
    async public Task<EmbedModel> Embed(string link, bool playerjs)
    {
        if (string.IsNullOrEmpty(link))
            return null;

        string news = await onget($"{apihost}/{link}");
        if (string.IsNullOrEmpty(news))
        {
            return null;
        }

        if (playerjs)
        {
            try
            {
                string video = Regex.Match(news, "id=\"playerjsfile\">([^<]+)<").Groups[1].Value;
                if (string.IsNullOrEmpty(video))
                {
                    if (news.Contains("<div class=\"alert\""))
                    {
                        var h3Match = Regex.Match(news, "<div class=\"alert\">\\s*<h3>([^<]+)</h3>");
                        return new EmbedModel() { IsEmpty = true, errormsg = h3Match.Success ? h3Match.Groups[1].Value.Trim() : "Ошибка alert" };
                    }

                    return null;
                }

                if (video.EndsWith("]"))
                {
                    var res = JsonSerializer.Deserialize<Season[]>(video, new JsonSerializerOptions
                    {
                        AllowTrailingCommas = true
                    });

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
                        return null;

                    var res = JsonSerializer.Deserialize<Season[]>(video, new JsonSerializerOptions
                    {
                        AllowTrailingCommas = true
                    });

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
                    return null;

                return new EmbedModel() { content = video };
            }
            #endregion
        }
    }
    #endregion

    #region Tpl
    public ITplResult Tpl(EmbedModel md, string title, string href, int s, string t, bool rjson = false)
    {
        if (md == null || md.IsEmpty)
            return default;

        if (md.content != null)
        {
            #region Фильм
            var mtpl = new MovieTpl(title);

            if (init.playerjs)
            {
                if (md.content.Contains("{"))
                {
                    var voices = md.content.Split("{");
                    var hash = new HashSet<string>(20);

                    foreach (string line in voices)
                    {
                        string voice = Regex.Match(line, "([^\\}]+)\\}").Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(voice) && !hash.Contains(voice))
                        {
                            hash.Add(voice);

                            var streamquality = new StreamQualityTpl();

                            foreach (string q in new string[] { "2160", "1440", "1080", "720", "480", "360" })
                            {
                                foreach (var line2 in voices)
                                {
                                    if (line2.Contains(voice) && (line2.Contains($"_{q}") || line2.Contains($"_{q}")))
                                    {
                                        string links = Regex.Match(line2, "\\}([^\\[,;]+)").Groups[1].Value;
                                        if (string.IsNullOrEmpty(links))
                                            continue;

                                        streamquality.Append(onstreamfile.Invoke(links), $"{q}p");
                                    }
                                }
                            }

                            var first = streamquality.Firts();
                            if (first != null)
                            {
                                mtpl.Append(
                                    voice,
                                    first.link,
                                    streamquality: streamquality
                                );
                            }
                        }
                    }
                }
                else
                {
                    var streamquality = new StreamQualityTpl();

                    foreach (string q in new string[] { "2160", "1440", "1080", "720", "480", "360" })
                    {
                        string link = Regex.Match(md.content, $"\\[{q}p\\]([^\\[,; ]+)").Groups[1].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        streamquality.Append(onstreamfile.Invoke(link), $"{q}p");
                    }

                    var first = streamquality.Firts();
                    if (first != null)
                    {
                        mtpl.Append(
                            "По умолчанию",
                            first.link,
                            streamquality: streamquality
                        );
                    }
                }
            }
            else
            {
                var streamquality = new StreamQualityTpl();

                foreach (string q in new string[] { "1080", "720", "480", "360" })
                {
                    string link = Regex.Match(md.content, $"(https?://[^\"\\[\\|,;\n\r\t ]+_{q}(_10)?.(mp4|m3u8))").Groups[1].Value;
                    if (string.IsNullOrEmpty(link))
                        continue;

                    streamquality.Append(onstreamfile.Invoke(link), $"{q}p");
                }

                var first = streamquality.Firts();
                if (first != null)
                {
                    mtpl.Append(
                        first.quality,
                        first.link,
                        streamquality: streamquality
                    );
                }
            }

            return mtpl;
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

                        tpl.Append(
                            "1 сезон",
                            host + $"lite/kinobase?title={enc_title}&href={HttpUtility.UrlEncode(href)}&t={HttpUtility.UrlEncode(t)}&s=1",
                            1
                        );

                        return tpl;
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

                            tpl.Append(
                                $"{season} сезон",
                                host + $"lite/kinobase?title={enc_title}&href={HttpUtility.UrlEncode(href)}&t={HttpUtility.UrlEncode(t)}&s={season}",
                                season
                            );
                        }

                        return tpl;
                    }
                    else
                    {
                        return renderSeason(md.serial.First(i => i.title.StartsWith($"{s} ")).folder, host, onstreamfile);
                    }
                }

                ITplResult renderSeason(Season[] episodes, string host, Func<string, string> onstreamfile)
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

                                vtpl.Append(
                                    voice,
                                    t == voice,
                                    host + $"lite/kinobase?title={enc_title}&href={HttpUtility.UrlEncode(href)}&t={HttpUtility.UrlEncode(voice)}&s={s}"
                                );
                            }

                            m = m.NextMatch();
                        }
                    }
                    #endregion

                    var etpl = new EpisodeTpl(vtpl, episodes.Length);

                    foreach (var episode in episodes)
                    {
                        if (string.IsNullOrEmpty(episode.file) || (t != null && !episode.file.Contains(t)))
                            continue;

                        var streamquality = new StreamQualityTpl();

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

                                streamquality.Append(onstreamfile.Invoke(links), $"{quality}p");
                            }
                            else
                            {
                                string links = Regex.Match(qline, "{" + Regex.Escape(t) + "}" + "([^,;]+)").Groups[1].Value;
                                if (string.IsNullOrEmpty(links))
                                    continue;

                                streamquality.Append(onstreamfile.Invoke(links), $"{quality}p");
                            }
                        }

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

                        var first = streamquality.Firts();
                        if (first != null)
                        {
                            etpl.Append(
                                episode.title,
                                title,
                                sArhc,
                                Regex.Match(episode.title,
                                "^([0-9]+)").Groups[1].Value,
                                first.link,
                                subtitles: subtitles,
                                streamquality: streamquality
                            );
                        }
                    }

                    return etpl;
                }
            }
            else
            {
                #region uppod.js
                ITplResult finEpisode(Season[] data, Func<string, string> onstreamfile)
                {
                    var etpl = new EpisodeTpl(data.Length);

                    foreach (var episode in data)
                    {
                        if (string.IsNullOrEmpty(episode.file))
                            continue;

                        var streamquality = new StreamQualityTpl();

                        foreach (string quality in new List<string> { "1080", "720", "480", "360" })
                        {
                            string link = Regex.Match(episode.file, $"(https?://[^\"\\[\\|,;\n\r\t ]+_{quality}.(mp4|m3u8))").Groups[1].Value;
                            if (string.IsNullOrEmpty(link))
                                continue;

                            streamquality.Append(onstreamfile.Invoke(link), $"{quality}p");
                        }

                        var first = streamquality.Firts();
                        if (first != null)
                        {
                            etpl.Append(
                                episode.title,
                                title,
                                sArhc,
                                Regex.Match(episode.title,
                                "^([0-9]+)").Groups[1].Value,
                                first.link,
                                streamquality: streamquality
                            );
                        }
                    }

                    return etpl;
                }

                if (md.serial.First().folder == null)
                {
                    if (s == -1)
                    {
                        var tpl = new SeasonTpl(md.quality);

                        tpl.Append(
                            "1 сезон",
                            host + $"lite/kinobase?title={enc_title}&href={HttpUtility.UrlEncode(href)}&s=1",
                            1
                        );

                        return tpl;
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

                            tpl.Append(
                                $"{season} сезон",
                                host + $"lite/kinobase?title={enc_title}&href={HttpUtility.UrlEncode(href)}&s={season}",
                                season
                            );
                        }

                        return tpl;
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

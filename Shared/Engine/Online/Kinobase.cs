using Lampac.Engine.CORE;
using Lampac.Models.LITE.Kinobase;
using Shared.Model.Base;
using Shared.Model.Online.Kinobase;
using Shared.Model.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class KinobaseInvoke
    {
        #region KinobaseInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;
        Action? requesterror;

        public KinobaseInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null, Action? requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.onpost = onpost;
            this.requesterror = requesterror;
        }
        #endregion

        #region Search
        async public ValueTask<SearchModel?> Search(string? title, int year)
        {
            if (string.IsNullOrEmpty(title))
                return null;

            string? content = await onget($"{apihost}/search?query={HttpUtility.UrlEncode(title)}");
            if (content == null)
            {
                requesterror?.Invoke();
                return null;
            }

            var rows = content.Split("<li class=\"item\">");
            string? link = null, reservedlink = null;

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

                if (name.ToLower().Trim() == title.ToLower())
                {
                    reservedlink = rlnk;
                    if (string.IsNullOrEmpty(link) && _year == year.ToString())
                        link = reservedlink;
                }
            }

            if (string.IsNullOrEmpty(link))
            {
                if (string.IsNullOrEmpty(reservedlink))
                {
                    if (!content.Contains(">По запросу") && similar.data.Count == 0)
                    {
                        requesterror?.Invoke();
                        return null;
                    }
                }

                link = reservedlink;
            }

            return new SearchModel() 
            {
                link = link,
                similar = similar
            };
        }
        #endregion

        #region Embed
        async public ValueTask<EmbedModel?> Embed(string? link)
        {
            if (string.IsNullOrEmpty(link))
                return null;

            string? news = await onget($"{apihost}/{link}");
            if (string.IsNullOrEmpty(news))
            {
                requesterror?.Invoke();
                return null;
            }

            if (news.Contains("id=\"playlists\""))
            {
                try
                {
                    string video = Regex.Match(news, "id=\"playlists\">([^<]+)<").Groups[1].Value;
                    var res = JsonSerializer.Deserialize<List<Season>>(video);
                    if (res == null || res.Count == 0)
                        return null;

                    return new EmbedModel() { serial = res, quality = video.Contains("1080.") ? "1080p" : video.Contains("720.") ? "720p" : video.Contains("480.") ? "480p" : "360p" };
                }
                catch { return null; }
            }
            else
            {
                string? video = StringConvert.FindLastText(news, "id=\"videoplayer\"", "</div>");
                if (string.IsNullOrEmpty(video))
                {
                    requesterror?.Invoke();
                    return null;
                }

                return new EmbedModel() { content = video };
            }
        }
        #endregion

        #region Html
        public string Html(EmbedModel? md, string? title, string href, int s, bool rjson = false)
        {
            if (md == null || md.IsEmpty)
                return string.Empty;

            if (md.content != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title);

                var streams = new List<(string link, string quality)>(4);

                foreach (string q in new string[] { "1080", "720", "480", "360" })
                {
                    string link = Regex.Match(md.content, $"(https?://[^\"\\[\\|,;\n\r\t ]+_{q}.(mp4|m3u8))").Groups[1].Value;
                    if (string.IsNullOrEmpty(link))
                        continue;

                    streams.Add((onstreamfile.Invoke(link), $"{q}p"));
                }

                if (streams.Count == 0)
                    return string.Empty;

                mtpl.Append(streams[0].quality, streams[0].link, streamquality: new StreamQualityTpl(streams));

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                string? enc_title = HttpUtility.UrlEncode(title);
                string sArhc = s.ToString();

                string finEpisode(List<Season> data)
                {
                    var etpl = new EpisodeTpl(data.Count);

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
                        return finEpisode(md.serial);
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
                        return finEpisode(md.serial.First(i => i.title.StartsWith($"{s} ")).folder);
                    }
                }
                #endregion
            }
        }
        #endregion
    }
}

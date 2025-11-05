using Shared.Models.Base;
using Shared.Models.Online.Eneyida;
using Shared.Models.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public struct EneyidaInvoke
    {
        #region EneyidaInvoke
        string host;
        string apihost;
        Func<string, ValueTask<string>> onget;
        Func<string, string, ValueTask<string>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string> onlog;
        Action requesterror;

        public EneyidaInvoke(string host, string apihost, Func<string, ValueTask<string>> onget, Func<string, string, ValueTask<string>> onpost, Func<string, string> onstreamfile, Func<string, string> onlog = null, Action requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onpost = onpost;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        public async ValueTask<EmbedModel> Embed(string original_title, int year, string href, bool similar)
        {
            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(original_title) || year == 0))
                return null;

            string link = href;
            var result = new EmbedModel();

            if (string.IsNullOrEmpty(link))
            {
                onlog?.Invoke("search start");
                string search = await onpost.Invoke($"{apihost}/index.php?do=search", $"do=search&subaction=search&search_start=0&result_from=1&story={HttpUtility.UrlEncode(original_title)}");
                if (search == null)
                {
                    requesterror?.Invoke();
                    return null;
                }

                onlog?.Invoke("search ok");

                var rows = search.Split("<article ");
                string stitle = StringConvert.SearchName(original_title?.ToLower());

                foreach (string row in rows.Skip(1))
                {
                    if (row.Contains(">Анонс</div>") || row.Contains(">Трейлер</div>"))
                        continue;

                    string newslink = Regex.Match(row, "href=\"(https?://[^/]+/[^\"]+\\.html)\"").Groups[1].Value;
                    if (string.IsNullOrEmpty(newslink))
                        continue;

                    // <div class="short_subtitle"><a href="https://eneyida.tv/xfsearch/year/2025/">2025</a> &bull; Thunderbolts</div>
                    var g = Regex.Match(row, "class=\"short_subtitle\">(<a [^>]+>([0-9]{4})</a>)?([^<]+)</div>").Groups;

                    string name = g[3].Value.Replace("&bull;", "").Trim();
                    if (string.IsNullOrEmpty(name))
                        continue;

                    if (result.similars == null)
                        result.similars = new List<Similar>(rows.Length);

                    string uaname = Regex.Match(row, "id=\"short_title\"[^>]+>([^<]+)<").Groups[1].Value;
                    string img = Regex.Match(row, "data-src=\"/([^\"]+)\"").Groups[1].Value;

                    result.similars.Add(new Similar()
                    {
                        title = $"{uaname} / {name}",
                        year = g[2].Value,
                        href = newslink,
                        img = string.IsNullOrEmpty(img) ? null : $"{apihost}/{img}"
                    });

                    if (StringConvert.SearchName(name) == stitle && g[2].Value == year.ToString())
                    {
                        link = newslink;
                        break;
                    }
                }

                if (similar)
                    return result;

                if (string.IsNullOrEmpty(link))
                {
                    if (result.similars.Count > 0)
                        return result;

                    if (search.Contains(">Пошук по сайту<"))
                        return new EmbedModel() { IsEmpty = true };

                    return null;
                }
            }

            onlog?.Invoke("link: " + link);
            string news = await onget.Invoke(link);
            if (news == null)
            {
                requesterror?.Invoke();
                return null;
            }

            if (news.Contains("full_content fx_row"))
                result.quel = Regex.Match(news.Split("full_content fx_row")[1].Split("full__favourite")[0], " (1080p|720p|480p)</div>").Groups[1].Value;

            string iframeUri = Regex.Match(news, "<iframe width=\"100%\" height=\"400\" src=\"(https?://[^/]+/[^\"]+/[0-9]+)\"").Groups[1].Value;
            if (string.IsNullOrEmpty(iframeUri))
                return null;

            onlog?.Invoke("iframeUri: " + iframeUri);
            string content = await onget.Invoke(iframeUri);
            if (content == null || !content.Contains("file:"))
            {
                requesterror?.Invoke();
                return null;
            }

            if (Regex.IsMatch(content, "file: ?'\\["))
            {
                Models.Online.Tortuga.Voice[] root = null;

                try
                {
                    root = JsonSerializer.Deserialize<Models.Online.Tortuga.Voice[]>(Regex.Match(content, "file: ?'([^\n\r]+)',").Groups[1].Value);
                    if (root == null || root.Length == 0)
                        return null;
                }
                catch { return null; }

                result.serial = root;
            }
            else
            {
                result.content = content;
            }

            return result;
        }
        #endregion

        #region Html
        public string Html(EmbedModel result, int clarification, string title, string original_title, int year, int t, int s, string href, VastConf vast = null, bool rjson = false)
        {
            if (result == null || result.IsEmpty)
                return string.Empty;

            string enc_title = HttpUtility.UrlEncode(title);
            string enc_original_title = HttpUtility.UrlEncode(original_title);

            #region similar
            if (result.content == null && result.serial == null)
            {
                if (string.IsNullOrWhiteSpace(href) && result.similars != null && result.similars.Count > 0)
                {
                    var stpl = new SimilarTpl(result.similars.Count);

                    foreach (var similar in result.similars)
                    {
                        string link = host + $"lite/eneyida?clarification={clarification}&title={enc_title}&original_title={enc_original_title}&year={year}&href={HttpUtility.UrlEncode(similar.href)}";

                        stpl.Append(similar.title, similar.year, string.Empty, link, PosterApi.Size(similar.img));
                    }

                    return rjson ? stpl.ToJson() : stpl.ToHtml();
                }

                return string.Empty;
            }
            #endregion

            if (result.content != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);

                string hls = Regex.Match(result.content, "file: ?\"(https?://[^\"]+/index.m3u8)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(hls))
                    return string.Empty;

                #region subtitle
                SubtitleTpl subtitles = new SubtitleTpl();
                string subtitle = new Regex("subtitle: ?\"([^\"]+)\"").Match(result.content).Groups[1].Value;

                if (!string.IsNullOrEmpty(subtitle))
                {
                    var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(subtitle);
                    while (match.Success)
                    {
                        subtitles.Append(match.Groups[1].Value, onstreamfile.Invoke(match.Groups[2].Value));
                        match = match.NextMatch();
                    }
                }
                #endregion

                mtpl.Append(string.IsNullOrEmpty(result.quel) ? "По умолчанию" : result.quel, onstreamfile.Invoke(hls), subtitles: subtitles, vast: vast);

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                #endregion
            }
            else
            {
                #region Сериал
                string enc_href = HttpUtility.UrlEncode(href);

                try
                {
                    if (s == -1)
                    {
                        #region Сезоны
                        var tpl = new SeasonTpl();

                        foreach (var season in result.serial)
                        {
                            string numberseason = Regex.Match(season.title, "^([0-9]+)").Groups[1].Value;
                            if (string.IsNullOrEmpty(numberseason))
                                continue;

                            string link = host + $"lite/eneyida?rjson={rjson}&clarification={clarification}&title={enc_title}&original_title={enc_original_title}&year={year}&href={enc_href}&s={numberseason}";

                            tpl.Append(season.title, link, numberseason);
                        }

                        return rjson ? tpl.ToJson() : tpl.ToHtml();
                        #endregion
                    }
                    else
                    {
                        var season = result.serial.First(i => i.title.StartsWith($"{s} "));

                        #region Перевод
                        var vtpl = new VoiceTpl();

                        for (int i = 0; i < season.folder.Length; i++)
                        {
                            if (t == -1)
                                t = i;

                            string link = host + $"lite/eneyida?rjson={rjson}&clarification={clarification}&title={enc_title}&original_title={enc_original_title}&year={year}&href={enc_href}&s={s}&t={i}";

                            vtpl.Append(season.folder[i].title, t == i, link);
                        }
                        #endregion

                        string sArch = s.ToString();
                        var episodes = season.folder[t].folder;

                        var etpl = new EpisodeTpl(episodes.Length);

                        foreach (var episode in episodes)
                        {
                            #region subtitle
                            SubtitleTpl? subtitles = null;

                            if (!string.IsNullOrEmpty(episode.subtitle))
                            {
                                var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(episode.subtitle);
                                subtitles = new SubtitleTpl(match.Length);

                                while (match.Success)
                                {
                                    subtitles.Value.Append(match.Groups[1].Value, onstreamfile.Invoke(match.Groups[2].Value));
                                    match = match.NextMatch();
                                }
                            }
                            #endregion

                            string file = onstreamfile.Invoke(episode.file);
                            etpl.Append(episode.title, title ?? original_title, sArch, Regex.Match(episode.title, "^([0-9]+)").Groups[1].Value, file, subtitles: subtitles, vast: vast);
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
using Shared.Engine.RxEnumerate;
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
        HttpHydra httpHydra;
        Func<string, string> onstreamfile;
        Func<string, string> onlog;
        Action requesterror;

        public EneyidaInvoke(string host, string apihost, HttpHydra httpHydra, Func<string, string> onstreamfile, Func<string, string> onlog = null, Action requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.httpHydra = httpHydra;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        public async Task<EmbedModel> Embed(string original_title, int year, string href, bool similar)
        {
            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(original_title) || year == 0))
                return null;

            string link = href;
            var result = new EmbedModel();

            if (string.IsNullOrEmpty(link))
            {
                onlog?.Invoke("search start");

                bool searchEmpty = false;
                string _site = apihost;

                await httpHydra.PostSpan($"{_site}/index.php?do=search", $"do=search&subaction=search&search_start=0&result_from=1&story={HttpUtility.UrlEncode(original_title)}", search => 
                {
                    if (search.IsEmpty)
                        return;

                    searchEmpty = search.Contains(">Пошук по сайту<", StringComparison.OrdinalIgnoreCase);

                    var rx = Rx.Split("<article ", search, 1);

                    string stitle = StringConvert.SearchName(original_title?.ToLower());

                    foreach (var row in rx.Rows())
                    {
                        if (row.Contains(">Анонс</div>") || row.Contains(">Трейлер</div>"))
                            continue;

                        string newslink = row.Match("href=\"(https?://[^/]+/[^\"]+\\.html)\"");
                        if (newslink == null)
                            continue;

                        // <div class="short_subtitle"><a href="https://eneyida.tv/xfsearch/year/2025/">2025</a> &bull; Thunderbolts</div>
                        var g = row.Groups("class=\"short_subtitle\">(<a [^>]+>([0-9]{4})</a>)?([^<]+)</div>");

                        string name = g[3].Value.Replace("&bull;", "").Trim();
                        if (string.IsNullOrEmpty(name))
                            continue;

                        if (result.similars == null)
                            result.similars = new List<Similar>(rx.Count);

                        string uaname = row.Match("id=\"short_title\"[^>]+>([^<]+)<");
                        string img = row.Match("data-src=\"/([^\"]+)\"");

                        result.similars.Add(new Similar()
                        {
                            title = $"{uaname} / {name}",
                            year = g[2].Value,
                            href = newslink,
                            img = string.IsNullOrEmpty(img) ? null : $"{_site}/{img}"
                        });

                        if (StringConvert.SearchName(name) == stitle && g[2].Value == year.ToString())
                        {
                            link = newslink;
                            break;
                        }
                    }
                });

                if (similar)
                    return result;

                if (string.IsNullOrEmpty(link))
                {
                    if (result.similars.Count > 0)
                        return result;

                    if (searchEmpty)
                        return new EmbedModel() { IsEmpty = true };

                    requesterror?.Invoke();
                    return null;
                }
            }

            onlog?.Invoke("link: " + link);

            string iframeUri = null;

            await httpHydra.GetSpan(link, news => 
            {
                if (news.IsEmpty)
                    return;

                if (news.Contains("full_content fx_row", StringComparison.Ordinal))
                    result.quel = Rx.Match(news, " (1080p|720p|480p)</div>");

                iframeUri = Rx.Match(news, "<iframe width=\"100%\" height=\"400\" src=\"(https?://[^/]+/[^\"]+/[0-9]+)\"");
            });

            if (iframeUri == null)
            {
                requesterror?.Invoke();
                return null;
            }

            onlog?.Invoke("iframeUri: " + iframeUri);

            await httpHydra.GetSpan(iframeUri, content => 
            {
                if (content.IsEmpty || !content.Contains("file:", StringComparison.Ordinal))
                    return;

                if (Regex.IsMatch(content, "file: ?'\\["))
                {
                    try
                    {
                        var root = JsonSerializer.Deserialize<Models.Online.Tortuga.Voice[]>(Rx.Match(content, "file: ?'([^\n\r]+)',"));
                        if (root != null && root.Length > 0)
                            result.serial = root;
                    }
                    catch { }
                }
                else
                {
                    result.content = content.ToString();
                }
            });

            if (result.serial == null && string.IsNullOrEmpty(result.content))
            {
                requesterror?.Invoke();
                return null;
            }

            return result;
        }
        #endregion

        #region Tpl
        public ITplResult Tpl(EmbedModel result, int clarification, string title, string original_title, int year, int t, int s, string href, VastConf vast = null, bool rjson = false)
        {
            if (result == null || result.IsEmpty)
                return default;

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

                    return stpl;
                }

                return default;
            }
            #endregion

            if (result.content != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);

                string hls = Regex.Match(result.content, "file: ?\"(https?://[^\"]+/index.m3u8)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(hls))
                    return default;

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

                return mtpl;
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

                        return tpl;
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

                        var etpl = new EpisodeTpl(vtpl, episodes.Length);

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

                        return etpl;
                    }
                }
                catch
                {
                    return default;
                }
                #endregion
            }
        }
        #endregion
    }
}
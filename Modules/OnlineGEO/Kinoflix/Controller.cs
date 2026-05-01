using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Shared;
using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.Services.HTML;
using Shared.Services.Utilities;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Shared.Services.RxEnumerate;

namespace Kinoflix;

public class KinoflixController : BaseOnlineController
{
    public KinoflixController() : base(ModInit.conf) { }

    [HttpGet]
    [Route("lite/kinoflix")]
    async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, string href, string t = null, int s = -1)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        rhubFallback:

        #region search
        if (string.IsNullOrEmpty(href))
        {
            string searchTitle = clarification == 1 ? title : (original_title ?? title);
            if (string.IsNullOrWhiteSpace(searchTitle))
                return OnError();

            var search = await InvokeCacheResult<SearchModel>($"kinoflix:search:{searchTitle}", TimeSpan.FromHours(4), async e =>
            {
                var card = await Search(searchTitle, title, original_title, year);
                if (card == null)
                    return e.Fail("search", refresh_proxy: true);

                return e.Success(card);
            });

            if (!search.IsSuccess)
            {
                if (IsRhubFallback(search))
                    goto rhubFallback;

                return OnError(search.ErrorMsg);
            }

            if (search.Value.link == null)
                return ContentTpl(search.Value.similar);

            href = search.Value.link;
        }
        #endregion

        #region cache
        var cache = await InvokeCacheResult<CardModel>(ipkey($"kinoflix:iframe:{href}"), 20, textJson: true, onget: async e =>
        {
            string iframeUri = null;
            await httpHydra.GetSpan($"{init.host}/{href}", spanAction: html =>
            {
                iframeUri = Rx.Match(html, "<iframe src=\"([^\"]+)\"");
            });

            if (string.IsNullOrWhiteSpace(iframeUri))
                return e.Fail("iframeUri", refresh_proxy: true);

            var card = new CardModel();

            await httpHydra.GetSpan(iframeUri, spanAction: html =>
            {
                if (html.Contains("\"folder\":", StringComparison.Ordinal))
                {
                    string json = Rx.Match(html, "new\\s+Playerjs\\s*\\(\\s*(\\{[\\s\\S]*?\\})\\s*\\)");
                    if (json != null)
                        card.seasons = JsonConvert.DeserializeObject<PlayerModel>(json)?.file;
                }
                else
                {
                    card.file = Rx.Match(html, "\"file\":\"([^\n\r]+)");
                }
            });

            if (string.IsNullOrWhiteSpace(card.file) && card.seasons == null)
                return e.Fail("file", refresh_proxy: true);

            return e.Success(card);
        });


        if (IsRhubFallback(cache))
            goto rhubFallback;
        #endregion

        return ContentTpl(cache, () =>
        {
            if (cache.Value.file != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);
                var headers_stream = httpHeaders(init.host, init.headers_stream);

                foreach (var v in GetStreams(cache.Value.file))
                {
                    var streamQuality = new StreamQualityTpl();

                    foreach (var l in v.Value)
                        streamQuality.Append(HostStreamProxy(l.link), qnormalize(l.quality));

                    var first = streamQuality.Firts();
                    if (first != null)
                    {
                        mtpl.Append(
                            v.Key,
                            first.link,
                            quality: first.quality,
                            streamquality: streamQuality,
                            headers: headers_stream,
                            vast: init.vast
                        );
                    }
                }

                return mtpl;
                #endregion
            }
            else
            {
                #region Сериал
                var seasons = cache.Value.seasons;
                if (seasons == null || seasons.Count == 0)
                    return null;

                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);
                string enc_href = HttpUtility.UrlEncode(href);

                if (s == -1)
                {
                    var tpl = new SeasonTpl();

                    foreach (var season in seasons)
                    {
                        string seasonName = season.title;
                        string seasonNum = Regex.Match(seasonName, "([0-9]+)").Groups[1].Value;

                        tpl.Append(
                            seasonName ?? $"{seasonNum} сезон",
                            $"{host}/lite/kinoflix?title={enc_title}&original_title={enc_original_title}&href={enc_href}&s={seasonNum}",
                            seasonNum
                        );
                    }

                    return tpl;
                }
                else
                {
                    string _s = s.ToString();
                    string defaultargs = $"&title={enc_title}&original_title={enc_original_title}&year={year}&href={enc_href}";

                    var season = seasons.FirstOrDefault(x => x.title.EndsWith($" {_s}"));
                    if (season == null || season.folder == null)
                        return null;

                    #region Озвучки
                    var vtpl = new VoiceTpl();
                    var voices = new HashSet<string>();

                    foreach (var episode in season.folder)
                    {
                        foreach (Match m in Regex.Matches(episode.file ?? string.Empty, "\\{([^}]+)\\}", RegexOptions.IgnoreCase))
                        {
                            string voice = m.Groups[1].Value;
                            if (!string.IsNullOrWhiteSpace(voice) && voices.Add(voice))
                            {
                                if (string.IsNullOrEmpty(t))
                                    t = voice;

                                string vlink = $"{host}/lite/kinoflix?s={s}&t={HttpUtility.UrlEncode(voice)}" + defaultargs;
                                vtpl.Append(
                                    voice,
                                    t == voice,
                                    vlink
                                );
                            }
                        }
                    }
                    #endregion

                    var etpl = new EpisodeTpl(vtpl);
                    var headers_stream = httpHeaders(init.host, init.headers_stream);

                    foreach (var episode in season.folder)
                    {
                        string epNum = Regex.Match(episode.title, "([0-9]+)").Groups[1].Value;

                        foreach (var v in GetStreams(episode.file))
                        {
                            if (v.Key == t)
                            {
                                var streamQuality = new StreamQualityTpl();

                                foreach (var l in v.Value)
                                    streamQuality.Append(HostStreamProxy(l.link), qnormalize(l.quality));

                                var first = streamQuality.Firts();
                                if (first != null)
                                {
                                    etpl.Append(
                                        episode.title,
                                        title ?? original_title,
                                        _s,
                                        epNum,
                                        first.link,
                                        streamquality: streamQuality,
                                        headers: headers_stream,
                                        vast: init.vast
                                    );
                                }
                            }
                        }
                    }

                    return etpl;
                }
                #endregion
            }
        });
    }


    #region Search
    async Task<SearchModel> Search(string query, string title, string original_title, int year)
    {
        SearchModel smd = null;

        await httpHydra.GetSpan($"{init.host}/filter-movies?type=search&search={HttpUtility.UrlEncode(query)}", html =>
        {
            string link = null;
            var similar = new SimilarTpl();

            string stitle = StringConvert.SearchName(title);
            string soriginal_title = StringConvert.SearchName(original_title);

            foreach (ReadOnlySpan<char> row in HtmlSpan.Nodes(html, "div", "class", "filter_cols", HtmlSpanTargetType.Exact))
            {
                ReadOnlySpan<char> card = HtmlSpan.Node(row, "div", "class", "popular-card__title", HtmlSpanTargetType.Exact);
                if (card.IsEmpty)
                    continue;

                string href = Rx.Match(card, " href=\"https?://[^/]+/([^\"#]+)");
                if (string.IsNullOrEmpty(href))
                    continue;

                string name = Rx.Match(card, "<p>([^<]+)</p>");
                if (string.IsNullOrEmpty(name))
                    continue;

                string orig_name = Rx.Match(card, "<span>([^<]+)</span>");
                string _year = Rx.Match(row, "class=\"year\">([0-9]{4})");

                string img = Rx.Match(row, "<img\\s+data-src=\"([^\"]+)\"");
                if (!string.IsNullOrEmpty(img))
                    img = Rx.Match(row, "<img src=\"([^\"]+)\"");

                string _l = $"{host}/lite/kinoflix?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&year={year}&href={HttpUtility.UrlEncode(href)}";
                similar.Append(orig_name != null ? $"{name} / {orig_name}" : name, _year, string.Empty, _l, PosterApi.Size(img));

                bool match = StringConvert.SearchName(name).Contains(stitle)
                    || (orig_name != null && StringConvert.SearchName(orig_name).Contains(soriginal_title));

                if (match && _year == year.ToString())
                    link = href;
            }

            if (similar.Length > 0)
            {
                smd = new SearchModel()
                {
                    link = link,
                    similar = similar
                };
            }
        });

        return smd;
    }
    #endregion

    #region GetStreams
    static Dictionary<string, List<(string quality, string link)>> GetStreams(string file)
    {
        var voices = new Dictionary<string, List<(string quality, string link)>>();

        foreach (string quality in new string[] { "4K", "HD", "SD" })
        {
            foreach (Match m in Regex.Matches(file, $"\\[{quality}\\]([^\n\r\\[]+)", RegexOptions.IgnoreCase))
            {
                string line = m.Groups[1].Value;
                if (string.IsNullOrEmpty(line))
                    continue;

                foreach (Match v in Regex.Matches(line, "\\{([^\\}]+)\\}(https?://[^;]+)", RegexOptions.IgnoreCase))
                {
                    string voice = v.Groups[1].Value.Trim();
                    string link = v.Groups[2].Value;

                    if (string.IsNullOrWhiteSpace(voice) || string.IsNullOrWhiteSpace(link))
                        continue;

                    if (quality == "4K" && (link.Contains("_HD.mp4") || link.Contains("_SD.mp4")))
                        continue;

                    if (!voices.TryGetValue(voice, out var current))
                    {
                        current = new List<(string quality, string link)>();
                        voices[voice] = current;
                    }

                    current.Add((quality, link));
                }
            }
        }

        return voices;
    }
    #endregion

    static string qnormalize(string quality) => quality switch
    {
        "4K" => "2160p",
        "HD" => "1080p",
        "SD" => "480p",
        _ => quality
    };
}

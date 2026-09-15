using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Kinoteatrkg;

public class KinoteatrkgController : BaseOnlineController
{
    const int MaxResolveCandidates = 6;

    sealed class SearchCandidate
    {
        public string id;
        public string title;
        public short year;
        public int order;
        public bool mult;
    }

    sealed class PlaybackSource
    {
        public string pageUrl { get; set; }
        public string streamUrl { get; set; }
        public string label { get; set; }
    }

    static readonly Regex PlayerSourceRegex = new(
        """class=["'][^"']*\bkt-view-player-box\b[^"']*["'][\s\S]{0,12000}?<(?:source|video)\b[^>]*\bsrc=["'](?<src>[^"']+\.mp4(?:\?[^"']*)?)["']""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    static readonly Regex Mp4AttributeRegex = new(
        """(?:src|data-src)=["'](?<src>[^"']+\.mp4(?:\?[^"']*)?)["']""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    static readonly Regex Mp4QuotedRegex = new(
        """["'](?<src>(?:(?:https?:)?//|/)[^"']+?\.mp4(?:\?[^"']*)?)["']""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    static readonly Regex PageTitleRegex = new(
        """<h1[^>]*>\s*(?<title>[^<]+?)(?:\s*<span[^>]*>\s*(?<original>[^<]*?)\s*</span>)?\s*</h1>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    static readonly Regex MultPageTitleRegex = new(
        """<h2\b[^>]*class=["'][^"']*\btext-danger\b[^"']*["'][^>]*>(?<title>[\s\S]*?)</h2>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    static readonly Regex YearRegex = new(
        """<span>\s*Год\s*</span>\s*<strong>\s*(?<year>(?:19|20)\d{2})\s*</strong>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    static readonly Regex MultYearRegex = new(
        """fa-camera-retro[^>]*>[\s\S]{0,80}?(?<year>(?:19|20)\d{2})""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    static readonly Regex QualityRegex = new(
        """<span>\s*Качество\s*</span>\s*<strong>\s*(?<quality>[^<]+?)\s*</strong>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    static readonly Regex SearchCardRegex = new(
        """<article\b[^>]*>(?<card>[\s\S]*?)</article>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    static readonly Regex SearchViewLinkRegex = new(
        """<a\b(?<attrs>[^>]*\bhref=["'][^"']*/site/(?<kind>view|mult)\?id=(?<id>\d+)[^"']*["'][^>]*)>(?<body>[\s\S]*?)</a>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    static readonly Regex CardMovieTitleRegex = new(
        """<h3\b[^>]*class=["'][^"']*\bkt-movie-title\b[^"']*["'][^>]*>(?<value>[\s\S]*?)</h3>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    static readonly Regex CardHeadingRegex = new(
        """<h[1-4]\b[^>]*>(?<value>[\s\S]*?)</h[1-4]>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    static readonly Regex AriaLabelRegex = new(
        """\baria-label=["'](?<value>[^"']+)["']""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    static readonly Regex ImageAltRegex = new(
        """<img\b[^>]*\balt=["'](?<value>[^"']+)["']""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    static readonly Regex AnyYearRegex = new(
        """(?<!\d)(?<year>(?:19|20)\d{2})(?!\d)""",
        RegexOptions.CultureInvariant
    );

    public KinoteatrkgController() : base(ModInit.conf) { }

    [HttpGet, Staticache(manually: true)]
    [Route("lite/kinoteatrkg")]
    async public Task<ActionResult> Index(string title, string original_title, short year = 0, string source = null, string id = null, bool rjson = false)
    {
        if (await IsRequestBlocked(rch: false))
            return badInitMsg;

        string providerId = !string.IsNullOrEmpty(source) && source.Equals("kinoteatrkg", StringComparison.OrdinalIgnoreCase)
            ? id
            : null;

        string cacheKey = $"kinoteatrkg:v9:view:{providerId}:{NormalizeTitle(title)}:{NormalizeTitle(original_title)}:{year}";
        var cache = await InvokeCacheResult<string>(cacheKey, 40, async e =>
        {
            var sources = await ResolveMovie(title, original_title, year, providerId);
            if (sources == null || sources.Count == 0)
                return e.Fail("search");

            return e.Success(JsonSerializer.Serialize(sources));
        });

        if (!cache.IsSuccess || string.IsNullOrEmpty(cache.Value))
            return OnError(cache.ErrorMsg);

        List<PlaybackSource> sources;
        try
        {
            sources = JsonSerializer.Deserialize<List<PlaybackSource>>(cache.Value);
        }
        catch (JsonException)
        {
            return OnError("cache");
        }

        if (sources == null || sources.Count == 0)
            return OnError("cache");

        var mtpl = new MovieTpl(title, original_title);
        foreach (var sourceItem in sources)
        {
            if (string.IsNullOrWhiteSpace(sourceItem.pageUrl) || string.IsNullOrWhiteSpace(sourceItem.streamUrl))
                continue;

            var streamHeaders = HeadersModel.Init("referer", sourceItem.pageUrl);
            string label = sourceItem.label;
            if (sources.Count == 1 && label == "Обычная")
                label = "Kinoteatr.kg";

            mtpl.Append(
                string.IsNullOrWhiteSpace(label) ? "Kinoteatr.kg" : label,
                HostStreamProxy(sourceItem.streamUrl, streamHeaders),
                vast: init.vast
            );
        }

        return ContentTpl(mtpl);
    }

    async Task<List<PlaybackSource>> ResolveMovie(string title, string originalTitle, short year, string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            var source = await ResolveById(new SearchCandidate { id = id.Trim(), title = title }, year);
            return source == null ? null : [source];
        }

        var triedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (string query in SearchQueries(title, originalTitle))
        {
            var sources = await ResolveCandidates(await SearchSuggest(query), query, year, triedIds);
            if (sources.Count > 0)
                return sources;

            sources = await ResolveCandidates(await SearchFull(query, year), query, year, triedIds);
            if (sources.Count > 0)
                return sources;
        }

        return null;
    }

    async Task<List<PlaybackSource>> ResolveCandidates(
        List<SearchCandidate> candidates,
        string query,
        short year,
        HashSet<string> triedIds)
    {
        if (candidates == null || candidates.Count == 0)
            return [];

        string normalizedQuery = NormalizeTitle(query);
        var sources = new List<PlaybackSource>(2);
        var labels = new HashSet<string>(StringComparer.Ordinal);

        var eligible = candidates
            .Where(i => year <= 0 || i.year <= 0 || i.year == year)
            .ToList();
        var exact = eligible
            .Where(i => NormalizeTitle(i.title) == normalizedQuery)
            .ToList();
        if (exact.Count > 0)
            eligible = exact;

        var ordered = eligible
            .OrderByDescending(i => NormalizeTitle(i.title) == normalizedQuery)
            .ThenByDescending(i => year > 0 && i.year == year)
            .ThenByDescending(i => IsTitleMatch(i.title, query))
            .ThenBy(i => i.order)
            .Take(MaxResolveCandidates);

        foreach (var candidate in ordered)
        {
            if (string.IsNullOrWhiteSpace(candidate.id) || !triedIds.Add(CandidateKey(candidate)))
                continue;

            var source = await ResolveById(candidate, year, query);
            if (source == null || !labels.Add(source.label))
                continue;

            sources.Add(source);
            if (sources.Count == 2)
                break;
        }

        return sources.OrderBy(i => i.label == "60 FPS").ToList();
    }

    async Task<List<SearchCandidate>> SearchSuggest(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        string url = $"{init.host.TrimEnd('/')}/site/suggest?term={Uri.EscapeDataString(query.Trim())}";
        string json = await httpHydra.Get(url, addheaders: HeadersModel.Init("referer", $"{init.host.TrimEnd('/')}/"));
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var result = new List<SearchCandidate>();
            int order = 0;

            AddSuggestCandidates(doc.RootElement, "films", mult: false, result, ref order);
            AddSuggestCandidates(doc.RootElement, "mults", mult: true, result, ref order);

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static void AddSuggestCandidates(
        JsonElement root,
        string property,
        bool mult,
        List<SearchCandidate> result,
        ref int order)
    {
        if (!root.TryGetProperty(property, out JsonElement items) || items.ValueKind != JsonValueKind.Array)
            return;

        foreach (JsonElement item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out JsonElement idElement) || !item.TryGetProperty("title", out JsonElement titleElement))
                continue;

            string id = idElement.ValueKind switch
            {
                JsonValueKind.String => idElement.GetString(),
                JsonValueKind.Number => idElement.GetRawText(),
                _ => null
            };
            string title = titleElement.ValueKind == JsonValueKind.String ? titleElement.GetString() : null;

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                continue;

            result.Add(new SearchCandidate
            {
                id = id,
                title = title.Trim(),
                order = order++,
                mult = mult
            });
        }
    }

    async Task<List<SearchCandidate>> SearchFull(string query, short expectedYear)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        string host = init.host.TrimEnd('/');
        string url = $"{host}/search?query={Uri.EscapeDataString(query.Trim())}";
        string html = await httpHydra.Get(url, addheaders: HeadersModel.Init("referer", $"{host}/"));
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var candidates = new List<SearchCandidate>();
        var byId = new Dictionary<string, SearchCandidate>(StringComparer.Ordinal);
        int order = 0;

        foreach (Match linkMatch in SearchViewLinkRegex.Matches(html))
        {
            string body = linkMatch.Groups["body"].Value;
            string attrs = linkMatch.Groups["attrs"].Value;
            bool mult = linkMatch.Groups["kind"].Value.Equals("mult", StringComparison.OrdinalIgnoreCase);

            AddSearchCandidate(
                candidates,
                byId,
                linkMatch.Groups["id"].Value,
                ExtractLinkTitle(attrs, body),
                ExtractCandidateYear(body),
                order++,
                query,
                mult
            );
        }

        foreach (Match cardMatch in SearchCardRegex.Matches(html))
        {
            string card = cardMatch.Groups["card"].Value;
            Match linkMatch = SearchViewLinkRegex.Match(card);
            if (!linkMatch.Success)
                continue;

            bool mult = linkMatch.Groups["kind"].Value.Equals("mult", StringComparison.OrdinalIgnoreCase);

            AddSearchCandidate(
                candidates,
                byId,
                linkMatch.Groups["id"].Value,
                ExtractSearchTitle(card, linkMatch),
                ExtractCandidateYear(card),
                order++,
                query,
                mult
            );
        }

        return RankFullCandidates(candidates, query, expectedYear);
    }

    static void AddSearchCandidate(
        List<SearchCandidate> candidates,
        Dictionary<string, SearchCandidate> byId,
        string id,
        string title,
        short year,
        int order,
        string query,
        bool mult)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        title = CleanText(title);
        string key = $"{(mult ? "mult" : "film")}:{id}";

        if (!byId.TryGetValue(key, out SearchCandidate existing))
        {
            var candidate = new SearchCandidate
            {
                id = id,
                title = title,
                year = year,
                order = order,
                mult = mult
            };

            byId[key] = candidate;
            candidates.Add(candidate);
            return;
        }

        if (TitleScore(title, query) > TitleScore(existing.title, query))
            existing.title = title;

        if (existing.year == 0 && year > 0)
            existing.year = year;
    }

    static List<SearchCandidate> RankFullCandidates(List<SearchCandidate> candidates, string query, short expectedYear)
    {
        if (candidates == null || candidates.Count == 0)
            return candidates;

        string normalizedQuery = NormalizeTitle(query);

        var exact = candidates
            .Where(i => !string.IsNullOrEmpty(normalizedQuery) && NormalizeTitle(i.title) == normalizedQuery)
            .Where(i => expectedYear <= 0 || i.year <= 0 || i.year == expectedYear)
            .OrderByDescending(i => expectedYear > 0 && i.year == expectedYear)
            .ThenBy(i => i.order)
            .ToList();

        if (exact.Count > 0)
            return exact;

        var titleMatches = candidates
            .Where(i => IsTitleMatch(i.title, query))
            .Where(i => expectedYear <= 0 || i.year <= 0 || i.year == expectedYear)
            .OrderByDescending(i => expectedYear > 0 && i.year == expectedYear)
            .ThenBy(i => i.order)
            .ToList();

        if (titleMatches.Count > 0)
            return titleMatches;

        return candidates
            .Where(i => expectedYear <= 0 || i.year <= 0 || i.year == expectedYear)
            .OrderByDescending(i => expectedYear > 0 && i.year == expectedYear)
            .ThenBy(i => i.order)
            .Take(MaxResolveCandidates)
            .ToList();
    }

    static string ExtractSearchTitle(string card, Match linkMatch)
    {
        Match titleMatch = CardMovieTitleRegex.Match(card);
        if (titleMatch.Success)
        {
            string value = CleanText(titleMatch.Groups["value"].Value);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        Match headingMatch = CardHeadingRegex.Match(card);
        if (headingMatch.Success)
        {
            string value = CleanText(headingMatch.Groups["value"].Value);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return ExtractLinkTitle(linkMatch.Groups["attrs"].Value, linkMatch.Groups["body"].Value);
    }

    static string ExtractLinkTitle(string attrs, string body)
    {
        Match ariaMatch = AriaLabelRegex.Match(attrs ?? string.Empty);
        if (ariaMatch.Success)
        {
            string value = CleanText(ariaMatch.Groups["value"].Value);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        Match headingMatch = CardHeadingRegex.Match(body ?? string.Empty);
        if (headingMatch.Success)
        {
            string value = CleanText(headingMatch.Groups["value"].Value);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        Match altMatch = ImageAltRegex.Match(body ?? string.Empty);
        if (altMatch.Success)
        {
            string value = CleanText(altMatch.Groups["value"].Value);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return CleanText(body);
    }

    static short ExtractCandidateYear(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return 0;

        string text = CleanText(html.Length > 1400 ? html.Substring(0, 1400) : html);
        Match match = AnyYearRegex.Match(text);

        return match.Success && short.TryParse(match.Groups["year"].Value, out short year)
            ? year
            : (short)0;
    }

    static int TitleScore(string title, string query)
    {
        if (string.IsNullOrWhiteSpace(title))
            return 0;

        string left = NormalizeTitle(title);
        string right = NormalizeTitle(query);

        if (!string.IsNullOrEmpty(right) && left == right)
            return 1000;

        if (IsTitleMatch(title, query))
            return 800;

        return Math.Max(1, 300 - Math.Min(title.Length, 299));
    }

    async Task<PlaybackSource> ResolveById(SearchCandidate candidate, short expectedYear, string expectedTitle = null)
    {
        if (candidate == null || string.IsNullOrWhiteSpace(candidate.id) || !candidate.id.All(char.IsDigit))
            return null;

        string host = init.host.TrimEnd('/');
        string pageUrl = $"{host}/site/{(candidate.mult ? "mult" : "view")}?id={candidate.id}";
        string html = await httpHydra.Get(pageUrl, addheaders: HeadersModel.Init("referer", $"{host}/"));
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var pageTitles = ExtractPageTitles(html, candidate.mult);

        if (!string.IsNullOrWhiteSpace(expectedTitle))
        {
            if (!IsTitleMatch(pageTitles.title, expectedTitle) && !IsTitleMatch(pageTitles.original, expectedTitle))
                return null;
        }

        short pageYear = 0;
        Match yearMatch = candidate.mult ? MultYearRegex.Match(html) : YearRegex.Match(html);
        if (yearMatch.Success)
            short.TryParse(yearMatch.Groups["year"].Value, out pageYear);

        if (expectedYear > 0 && pageYear != expectedYear)
            return null;

        string streamUrl = ExtractStreamUrl(html, host);
        if (string.IsNullOrWhiteSpace(streamUrl))
            return null;

        string siteQuality = null;
        Match qualityMatch = QualityRegex.Match(html);
        if (qualityMatch.Success)
        {
            siteQuality = WebUtility.HtmlDecode(qualityMatch.Groups["quality"].Value);
            if (!string.IsNullOrWhiteSpace(siteQuality))
                siteQuality = Regex.Replace(siteQuality, """\s+""", " ").Trim();
        }

        if (!await IsStreamAvailable(streamUrl, pageUrl))
            return null;

        return new PlaybackSource
        {
            pageUrl = pageUrl,
            streamUrl = streamUrl,
            label = Is60Fps(candidate.title, pageTitles.title, siteQuality) ? "60 FPS" : "Обычная"
        };
    }

    static (string title, string original) ExtractPageTitles(string html, bool mult)
    {
        if (!mult)
        {
            Match titleMatch = PageTitleRegex.Match(html);
            return titleMatch.Success
                ? (
                    WebUtility.HtmlDecode(titleMatch.Groups["title"].Value)?.Trim(),
                    WebUtility.HtmlDecode(titleMatch.Groups["original"].Value)?.Trim()
                )
                : default;
        }

        Match multTitleMatch = MultPageTitleRegex.Match(html);
        if (!multTitleMatch.Success)
            return default;

        string combined = CleanText(multTitleMatch.Groups["title"].Value);
        int separator = combined.IndexOf('/', StringComparison.Ordinal);
        return separator > 0
            ? (combined[..separator].Trim(), combined[(separator + 1)..].Trim())
            : (combined, null);
    }

    async Task<bool> IsStreamAvailable(string streamUrl, string pageUrl)
    {
        using var response = await Http.ResponseHeaders(
            streamUrl,
            timeoutSeconds: 8,
            headers: HeadersModel.Init("referer", pageUrl),
            httpversion: init.httpversion,
            allowAutoRedirect: true,
            proxy: proxy
        );

        return response?.IsSuccessStatusCode == true;
    }

    static bool Is60Fps(params string[] values)
        => values.Any(value => !string.IsNullOrWhiteSpace(value)
            && Regex.IsMatch(value, """\b60\s*fps\b""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

    static string CandidateKey(SearchCandidate candidate)
        => $"{(candidate.mult ? "mult" : "film")}:{candidate.id}";

    static string ExtractStreamUrl(string html, string host)
    {
        if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(host))
            return null;

        Uri baseUri;
        try
        {
            baseUri = new Uri($"{host.TrimEnd('/')}/");
        }
        catch (UriFormatException)
        {
            return null;
        }

        Match playerMatch = PlayerSourceRegex.Match(html);
        if (playerMatch.Success)
        {
            string playerUrl = ResolveMp4Source(playerMatch.Groups["src"].Value, baseUri, requireVideoPath: false);
            if (!string.IsNullOrWhiteSpace(playerUrl))
                return playerUrl;
        }

        foreach (Match match in Mp4AttributeRegex.Matches(html))
        {
            string url = ResolveMp4Source(match.Groups["src"].Value, baseUri, requireVideoPath: true);
            if (!string.IsNullOrWhiteSpace(url))
                return url;
        }

        foreach (Match match in Mp4QuotedRegex.Matches(html))
        {
            string url = ResolveMp4Source(match.Groups["src"].Value, baseUri, requireVideoPath: true);
            if (!string.IsNullOrWhiteSpace(url))
                return url;
        }

        return null;
    }

    static string ResolveMp4Source(string rawSource, Uri baseUri, bool requireVideoPath)
    {
        string source = WebUtility.HtmlDecode(rawSource)?.Trim();
        if (string.IsNullOrWhiteSpace(source))
            return null;

        try
        {
            Uri resolved;
            if (Uri.TryCreate(source, UriKind.Absolute, out Uri absolute) &&
                (absolute.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                 absolute.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                resolved = absolute;
            }
            else
            {
                resolved = new Uri(baseUri, source);
            }

            if (!resolved.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !resolved.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return null;

            string path = resolved.AbsolutePath;
            if (!path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                return null;

            if (path.Contains("trailer", StringComparison.OrdinalIgnoreCase))
                return null;

            if (requireVideoPath && !path.Contains("/video", StringComparison.OrdinalIgnoreCase))
                return null;

            return resolved.AbsoluteUri;
        }
        catch (UriFormatException)
        {
            return null;
        }
    }

    static IEnumerable<string> SearchQueries(string title, string originalTitle)
    {
        if (!string.IsNullOrWhiteSpace(title))
            yield return title.Trim();

        if (!string.IsNullOrWhiteSpace(originalTitle) && !NormalizeTitle(originalTitle).Equals(NormalizeTitle(title), StringComparison.Ordinal))
            yield return originalTitle.Trim();
    }

    static bool IsTitleMatch(string candidate, string query)
    {
        string left = NormalizeTitle(candidate);
        string right = NormalizeTitle(query);

        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return false;

        return left == right || left.Contains(right, StringComparison.Ordinal) || right.Contains(left, StringComparison.Ordinal);
    }

    static string CleanText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string text = Regex.Replace(value, """<[^>]+>""", " ", RegexOptions.CultureInvariant);
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, """\s+""", " ").Trim();
    }

    static string NormalizeTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string decoded = WebUtility.HtmlDecode(value).ToLowerInvariant();
        decoded = Regex.Replace(
            decoded,
            """\b(?:full\s*hd|uhd|4k|60\s*fps|hd)\b""",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        return Regex.Replace(decoded, """[\W_]+""", string.Empty, RegexOptions.CultureInvariant);
    }
}

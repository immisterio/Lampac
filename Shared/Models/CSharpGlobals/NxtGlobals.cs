using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;
using Shared.Engine;
using Shared.Models.SISI.Base;
using Shared.Models.SISI.NextHUB;

namespace Shared.Models.CSharpGlobals
{
    public record NxtPlaylist(NxtSettings init, string plugin, string host, string html, HtmlDocument doc, List<PlaylistItem> playlists);

    public record NxtChangePlaylis(NxtSettings init, string plugin, string host, string html, HtmlNodeCollection nodes, PlaylistItem pl, HtmlNode row);

    public record NxtRoute(IRoute route, IQueryCollection query, string requestUrl, string search, string sort, string cat, string model, int page);

    public record NxtEvalView(NxtSettings init, IQueryCollection query, string html, string plugin, string url, string file, List<HeadersModel> headers, ProxyManager proxyManager);

    public record NxtRegexMatch(string html, RegexMatchSettings m);

    public record NxtMenuRoute(string host, string plugin, string url, string search, string cat, string sort, string model, IQueryCollection query, int page);

    public record NxtUrlRequest(string host, string plugin, string url, IQueryCollection query, bool related);

    public record NxtNodeValue(string value, string host);
}

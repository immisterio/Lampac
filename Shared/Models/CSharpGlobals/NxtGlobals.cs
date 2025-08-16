using HtmlAgilityPack;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;
using Shared.Model.Online;
using Shared.Model.SISI.NextHUB;

namespace Shared.Models.CSharpGlobals
{
    public record NxtPlaylist(NxtSettings init, string plugin, string host, string html, HtmlDocument doc, List<PlaylistItem> playlists);

    public record NxtChangePlaylis(NxtSettings init, string plugin, string host, string html, HtmlNodeCollection nodes, PlaylistItem pl, HtmlNode row);

    public record NxtRoute(IRoute route, string requestUrl, string search, string sort, string cat, int page);

    public record NxtFindStreamFile(string html, string plugin, string url);

    public record NxtChangeStreamFile(string file, List<HeadersModel> headers);

    public record NxtMenuRoute(string host, string plugin, string url, string search, string cat, string sort, string model, IQueryCollection query, int page);
}

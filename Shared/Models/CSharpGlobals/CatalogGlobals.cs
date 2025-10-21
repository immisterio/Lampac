using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Shared.Models.Catalog;

namespace Shared.Models.CSharpGlobals
{
    public record CatalogPlaylist(CatalogSettings init, string plugin, string host, string html, HtmlDocument doc, List<PlaylistItem> playlists);

    public record CatalogChangePlaylis(CatalogSettings init, string plugin, string host, string html, HtmlNodeCollection nodes, PlaylistItem pl, HtmlNode row);

    public record CatalogGlobalsMenuRoute(string host, string plugin, string url, string search, string cat, string sort, IQueryCollection query, int page);

    public record CatalogNodeValue(string value, string host);
}

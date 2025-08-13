using HtmlAgilityPack;
using Lampac.Models.SISI;
using Microsoft.Playwright;
using Shared.Model.Online;
using Shared.Model.SISI.NextHUB;
using System.Collections.Generic;

namespace Shared.Models.CSharpGlobals
{
    public record NxtChangePlaylis(string html, string host, NxtSettings init, PlaylistItem pl, HtmlNodeCollection nodes, HtmlNode row);

    public record NxtRoute(IRoute route, string search, string sort, string cat, int page);

    public record NxtFindStreamFile(string html, string plugin, string url);

    public record NxtChangeStreamFile(string file, List<HeadersModel> headers);

    public record NxtMenuRoute(string host, string url, string cat, string sort, int page);
}

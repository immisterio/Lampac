using Shared.Services;
using Shared.Services.HTML;
using Shared.Services.HTTP;
using Shared.Services.RxEnumerate;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace UaKino;

public struct UaKinoInvoke
{
    #region UaKinoInvoke
    string apihost;
    HttpHydra httpHydra;

    public UaKinoInvoke(string apihost, HttpHydra httpHydra)
    {
        this.apihost = apihost;
        this.httpHydra = httpHydra;
    }
    #endregion

    #region Search
    public async Task<EmbedModel> Search(string story)
    {
        string search = await PlaywrightHttp.Get(ModInit.conf, $"{apihost}/index.php?do=search&subaction=search&search_start=0&full_search=0&story={HttpUtility.UrlEncode(story)}");
        if (string.IsNullOrEmpty(search))
            return null;

        var result = new EmbedModel()
        {
            similars = new List<Similar>()
        };

        var nodes = HtmlSpan.Nodes(search, "div", "class", "item expand-link grid-items__item", HtmlSpanTargetType.Exact);

        foreach (var row in nodes)
        {
            string newslink = Rx.Match(row, "href=\"(https?://[^/]+)?/([^\"]+\\.html)\"", 2);
            if (newslink == null)
                continue;

            string title = Rx.Match(row, "<a class=\"item__title [^\"]+\"[^>]+>([^<]+)</a>");
            if (title == null)
                continue;

            string img =
                Rx.Match(row, "data-src=\"/([^\"]+)\"") ??
                Rx.Match(row, "src=\"/([^\"]+)\"");

            result.similars.Add(new Similar()
            {
                title = title,
                href = newslink,
                img = img == null ? null : $"{apihost}/{img}"
            });
        }

        if (result.similars == null || result.similars.Count == 0)
        {
            bool searchEmpty =
                search.Contains("Поиск по сайту", StringComparison.OrdinalIgnoreCase) ||
                search.Contains("знайдено 0", StringComparison.OrdinalIgnoreCase);

            return searchEmpty ? new EmbedModel() { IsEmpty = true } : null;
        }

        return result;
    }
    #endregion

    #region Embed
    public async Task<string> Embed(string href)
    {
        string news = await PlaywrightHttp.Get(ModInit.conf, $"{apihost}/{href}");
        if (string.IsNullOrEmpty(news))
            return null;

        return Regex.Match(news, "<iframe[^>]+src=\"(https?://hdvbua\\.[a-z]+/embed/[^\"]+)\"").Groups[1].Value;
    }
    #endregion
}

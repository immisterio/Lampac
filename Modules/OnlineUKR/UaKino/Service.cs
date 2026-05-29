using Shared.Services;
using Shared.Services.HTML;
using Shared.Services.HTTP;
using Shared.Services.RxEnumerate;
using System;
using System.Collections.Generic;
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
        var result = new EmbedModel()
        {
            similars = new List<Similar>()
        };

        bool searchEmpty = false;
        string _imghost = apihost;

        await PlaywrightHttp.GetSpan(ModInit.conf.plugin, $"{apihost}/index.php?do=search&subaction=search&search_start=0&full_search=0&story={HttpUtility.UrlEncode(story)}", search =>
        {
            searchEmpty =
                search.Contains("Поиск по сайту", StringComparison.OrdinalIgnoreCase) ||
                search.Contains("знайдено 0", StringComparison.OrdinalIgnoreCase);

            foreach (var row in HtmlSpan.Nodes(search, "div", "class", "item expand-link grid-items__item", HtmlSpanTargetType.Exact))
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
                    img = img == null ? null : $"{_imghost}/{img}"
                });
            }
        });

        if (result.similars == null || result.similars.Count == 0)
            return searchEmpty ? new EmbedModel() { IsEmpty = true } : null;

        return result;
    }
    #endregion

    #region Embed
    public async Task<string> Embed(string href)
    {
        string iframeUri = null;

        await PlaywrightHttp.GetSpan(ModInit.conf.plugin, $"{apihost}/{href}", html =>
        {
            iframeUri = Rx.Match(html, "<iframe[^>]+src=\"(https?://hdvbua\\.[a-z]+/embed/[^\"]+)\"");
        });

        return iframeUri;
    }
    #endregion
}

using Shared.Services.RxEnumerate;
using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.Services;
using Shared.Services.HTML;
using Shared.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;

namespace iRemux;

public struct iRemuxInvoke
{
    #region iRemuxInvoke
    string host, apihost;
    HttpHydra httpHydra;
    List<HeadersModel> authHeaders;
    Func<string, string> onstreamfile;

    public iRemuxInvoke(string host, string apihost, HttpHydra httpHydra, Func<string, string> onstreamfile, string cookie = null)
    {
        this.host = host != null ? $"{host}/" : null;
        this.apihost = apihost;
        this.httpHydra = httpHydra;
        this.onstreamfile = onstreamfile;
        if (cookie != null)
            authHeaders = HeadersModel.Init("Cookie", cookie);
    }
    #endregion

    #region Embed
    async public Task<EmbedModel> Embed(string title, string original_title, int year, string link)
    {
        var result = new EmbedModel();

        if (string.IsNullOrEmpty(link))
        {
            bool reqOk = false;

            string uri = $"{apihost}/index.php?do=search&subaction=search&from_page=0&story={HttpUtility.UrlEncode(title ?? original_title)}";

            await httpHydra.GetSpan(uri, addheaders: authHeaders, spanAction: search =>
            {
                reqOk = search.Contains(">Поиск по сайту<", StringComparison.OrdinalIgnoreCase);

                string stitle = SearchNameTo.Convert(title);
                string sorigtitle = SearchNameTo.Convert(original_title);

                var rx = Rx.Split("item--announce", search, 1);

                foreach (var row in rx.Rows())
                {
                    var g = row.Groups("class=\"item__title( [^\"]+)?\"><a href=\"(?<link>https?://[^\"]+)\">(?<name>[^<]+)</a>");

                    string name = g["name"].Value.ToLower();
                    if (string.IsNullOrWhiteSpace(name) || name.Contains("сезон") || name.Contains("серии") || name.Contains("серия"))
                        continue;

                    if (string.IsNullOrEmpty(g["link"].Value))
                        continue;

                    bool find = false;

                    if (SearchNameTo.Contains(name, stitle) ||
                        SearchNameTo.Contains(name, sorigtitle))
                        find = true;

                    if (find && name.Contains($"({year}/"))
                    {
                        result.similars.Add(new Similar()
                        {
                            title = name,
                            year = year.ToString(),
                            href = g["link"].Value
                        });
                    }
                }
            });

            if (result.similars.Count == 0)
            {
                if (reqOk)
                    return new EmbedModel() { IsEmpty = true };

                return null;
            }

            if (result.similars.Count > 1)
                return result;

            link = result.similars[0].href;
        }


        await httpHydra.GetSpan(link, addheaders: authHeaders, spanAction: news =>
        {
            var page_desc = HtmlSpan.Node(news, "div", "class", "page__desc", HtmlSpanTargetType.Exact);

            foreach (var node in HtmlSpan.Nodes(page_desc, "div", "class", "quote", HtmlSpanTargetType.Exact))
            {
                string linkid = Rx.Match(node, "href=\"https?://cloud.mail.ru/public/([^\"]+)\"");
                if (string.IsNullOrEmpty(linkid))
                    continue;

                bool setAuto = true;

                foreach (string q in new string[] { "2160p", "1080p", "720p", "480p" })
                {
                    string _qs = q == "480p" ? "1400" : q;
                    if (node.Contains(_qs, StringComparison.Ordinal))
                    {
                        result.links.Add(new PlayLinks() { linkid = linkid, quality = q });
                        setAuto = false;
                        break;
                    }
                }

                if (setAuto)
                    result.links.Add(new PlayLinks() { linkid = linkid, quality = "480p" });
            }
        });

        if (result.links.Count == 0)
            return null;

        result.links.Reverse();

        return result;
    }
    #endregion

    #region Weblink
    async public Task<string> Weblink(string linkid)
    {
        string location = null;

        await httpHydra.GetSpan($"https://cloud.mail.ru/public/{linkid}", html =>
        {
            var rx = Rx.Split("\"weblink_get\":", html, 1);
            if (rx.Count > 0)
                location = rx[0].Match("\"url\": ?\"(https?://[^/]+)");
        });

        if (string.IsNullOrEmpty(location))
            return null;

        return $"{location}/weblink/view/{linkid}";
    }
    #endregion

    #region Tpl
    public ITplResult Tpl(EmbedModel result, string title, string original_title, short year)
    {
        if (result == null || result.IsEmpty)
            return default;

        string enc_title = HttpUtility.UrlEncode(title);
        string enc_original_title = HttpUtility.UrlEncode(original_title);

        if (result.links.Count == 0)
        {
            #region similar
            if (result.similars != null && result.similars.Count > 0)
            {
                var stpl = new SimilarTpl(result.similars.Count);

                foreach (var similar in result.similars)
                {
                    stpl.Append(
                        similar.title,
                        similar.year,
                        string.Empty,
                        host + $"lite/remux?title={enc_title}&original_title={enc_original_title}&year={year}&href={HttpUtility.UrlEncode(similar.href)}"
                    );
                }

                return stpl;
            }

            return default;
            #endregion
        }
        else
        {
            var mtpl = new MovieTpl(title, original_title, result.links.Count);

            foreach (var link in result.links)
            {
                mtpl.Append(
                    link.quality,
                    host + $"lite/remux/movie?linkid={link.linkid}&quality={link.quality}&title={enc_title}&original_title={enc_original_title}",
                    "call"
                );
            }

            return mtpl;
        }
    }
    #endregion
}

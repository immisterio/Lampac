using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.Services.HTML;
using Shared.Services.RxEnumerate;
using Shared.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace LeProduction;

public class LeProductionController : BaseOnlineController
{
    public LeProductionController() : base(ModInit.conf) { }

    [HttpGet, Staticache(manually: true)]
    [Route("lite/leproduction")]
    async public Task<ActionResult> Index(string title, string original_title, byte clarification, byte serial, bool similar = false, string href = null)
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

            var search = await InvokeCacheResult<(string href, List<SimilarDto> similar)>($"leproduction:search:{searchTitle}", TimeSpan.FromHours(4), async e =>
            {
                string newsHref = null;
                var similar = new List<SimilarDto>();

                await httpHydra.GetSpan($"{init.host}/index.php?do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(searchTitle)}",
                    spanAction: html =>
                {
                    string stitle = SearchNameTo.Convert(title);
                    string soriginal = SearchNameTo.Convert(original_title);

                    foreach (ReadOnlySpan<char> row in HtmlSpan.Nodes(html, "div", "class", "short-item", HtmlSpanTargetType.Exact))
                    {
                        string link = Rx.Match(row, "href=\"https?://[^/]+/([^\"]+\\.html)\"");
                        string name = Rx.Match(row, ">([^<]+)</a></h3>");

                        if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(name))
                            continue;

                        string img = Rx.Match(row, "src=\"/([^\"]+)\"");
                        if (img != null)
                            img = $"{init.host}/{img}";

                        similar.Add(new SimilarDto(
                            link,
                            0,
                            string.Empty,
                            name,
                            img
                        ));

                        if (newsHref == null)
                        {
                            if (SearchNameTo.Contains(name, stitle) ||
                                SearchNameTo.Contains(name, soriginal))
                                newsHref = link;
                        }
                    }
                });

                if (newsHref == null && similar.Count > 0)
                    return e.Success((null, similar));

                if (string.IsNullOrWhiteSpace(newsHref))
                    return e.Fail("search", refresh_proxy: true);

                return e.Success((newsHref, similar));
            });

            if (!search.IsSuccess)
            {
                if (IsRhubFallback(search))
                    goto rhubFallback;

                return OnError(search.ErrorMsg);
            }

            if (search.Value.href == null || similar || serial == 1)
            {
                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);

                var stpl = new SimilarTpl(search.Value.similar.Count);

                foreach (var sim in search.Value.similar)
                {
                    stpl.Append(
                        sim.title,
                        string.Empty,
                        string.Empty,
                        $"{host}/lite/leproduction?href={HttpUtility.UrlEncode(sim.url)}&title={enc_title}&original_title={enc_original_title}",
                        PosterApi.Size(sim.img)
                    );
                }

                return ContentTpl(stpl);
            }

            href = search.Value.href;
        }
        #endregion

        #region iframe
        var cache = await InvokeCacheResult<EmbedModel>($"leproduction:view:{href}", 20, async e =>
        {
            string iframe = null;

            await httpHydra.GetSpan($"{init.host}/{href}", spanAction: html =>
            {
                iframe =
                    Rx.Match(html, "<iframe[^>]+id=\"omfg\"[^>]+src=\"([^\"]+)\"") ??
                    Rx.Match(html, "<iframe[^>]+src=\"(https?://[^/]+/playlist_iframe/[0-9]+/?[^\"]*)\"");
            });

            if (iframe != null)
            {
                EmbedModel result = null;

                await httpHydra.GetSpan(iframe, addheaders: HeadersModel.Init("referer", $"{init.host}/{href}"), spanAction: html =>
                {
                    ReadOnlySpan<char> fileBlock = Rx.Slice(html, "file:", "embed:");

                    if (fileBlock.Contains("серия", StringComparison.Ordinal))
                    {
                        ReadOnlySpan<char> json = fileBlock.Trim()[..^1];

                        var root = JsonSerializer.Deserialize<SerialModel[]>(json, new JsonSerializerOptions
                        {
                            AllowTrailingCommas = true
                        });

                        if (root != null && root.Length > 0)
                        {
                            result = new EmbedModel()
                            {
                                serial = root,
                                season = Regex.Match(root[0].comment, " ([0-9]+) сезон").Groups[1].Value
                            };
                        }
                    }
                    else
                    {
                        var match = Regex.Match(fileBlock.ToString(), "\\[([0-9]+p)\\](https?://[^,\\[\"\t\n ]+)");

                        if (match.Success)
                        {
                            var streams = new List<StreamQualityDto>();

                            while (match.Success)
                            {
                                streams.Add(new StreamQualityDto(
                                    match.Groups[2].Value,
                                    match.Groups[1].Value
                                ));

                                match = match.NextMatch();
                            }

                            result = new EmbedModel()
                            {
                                movie = streams
                            };
                        }
                    }
                });

                if (result != null)
                    return e.Success(result);
            }

            return e.Fail("view", refresh_proxy: true);
        });

        if (IsRhubFallback(cache))
            goto rhubFallback;
        #endregion

        return ContentTpl(cache, () =>
        {
            if (cache.Value.movie != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, 1);

                var first = cache.Value.movie[0];

                mtpl.Append(
                    "LE-Production",
                    HostStreamProxy(first.link),
                    quality: first.quality,
                    streamquality: new StreamQualityTpl(cache.Value.movie, linkPredicate: link => HostStreamProxy(link)),
                    vast: init.vast
                );

                return mtpl;
                #endregion
            }
            else
            {
                #region Сериал
                var etpl = new EpisodeTpl();

                foreach (var episode in cache.Value.serial)
                {
                    if (episode.streams == null)
                    {
                        string file = episode.file;
                        if (string.IsNullOrEmpty(file))
                            continue;

                        episode.streams = new List<StreamQualityDto>(3);

                        foreach (string q in new string[] { "1080", "720", "360" })
                        {
                            var g = new Regex($"{q}p?\\](?<file>https?://[^,\\[\"\t\n\r ]+)").Match(file).Groups;
                            if (!string.IsNullOrEmpty(g["file"].Value))
                            {
                                episode.streams.Add(new StreamQualityDto(
                                    g["file"].Value,
                                    $"{q}p"
                                ));
                            }
                        }
                    }

                    if (episode.streams.Count > 0)
                    {
                        string e = Regex.Match(episode.comment, " ([0-9]+) серия").Groups[1].Value;

                        etpl.Append(
                            $"{e} серия",
                            title ?? original_title,
                            cache.Value.season,
                            e,
                            HostStreamProxy(episode.streams[0].link),
                            streamquality: new StreamQualityTpl(episode.streams, linkPredicate: link => HostStreamProxy(link)),
                            vast: init.vast
                        );
                    }
                }

                return etpl;
                #endregion
            }
        });
    }
}

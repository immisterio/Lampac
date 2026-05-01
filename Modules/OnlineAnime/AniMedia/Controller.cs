using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.Services.RxEnumerate;
using Shared.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace AniMedia;

public class AniMediaController : BaseOnlineController
{
    public AniMediaController() : base(ModInit.conf) { }

    [HttpGet]
    [Staticache]
    [Route("lite/animedia")]
    async public Task<ActionResult> Index(string title, string news, bool rjson = false, bool similar = false)
    {
        if (await IsRequestBlocked(rch: false))
            return badInitMsg;

        if (string.IsNullOrEmpty(news))
        {
            if (string.IsNullOrEmpty(title))
                return OnError();

            #region Поиск
            var cache = await InvokeCacheResult<List<(string title, string url, string img)>>($"animedia:search:{title}:{similar}", TimeSpan.FromHours(4), async e =>
            {
                bool reqOk = false;
                List<(string title, string url, string img)> catalog = null;

                await httpHydra.PostSpan($"{init.host}/index.php?do=search", $"do=search&subaction=search&from_page=0&story={HttpUtility.UrlEncode(title)}", search =>
                {
                    reqOk = search.Contains("id=\"dosearch\"", StringComparison.Ordinal);

                    var article = Rx.Split("</article>", search);
                    if (article.Count > 1)
                    {
                        var rx = Rx.Split("grid-item d-flex fd-column", article[1].Span, 1);

                        catalog = new List<(string title, string url, string img)>(rx.Count);

                        foreach (var row in rx.Rows())
                        {
                            var g = row.Groups("<a href=\"https?://[^/]+/([^\"]+)\" class=\"poster__link\"><h3 class=\"poster__title line-clamp\">([^<]+)</h3></a>");

                            if (!string.IsNullOrEmpty(g[1].Value) && !string.IsNullOrEmpty(g[2].Value))
                            {
                                string img = row.Match("<img src=\"([^\"]+)\"");
                                if (img != null)
                                    img = init.host + img;

                                if (similar || StringConvert.SearchName(g[2].Value).Contains(StringConvert.SearchName(title)))
                                    catalog.Add((g[2].Value, g[1].Value, img));
                            }
                        }
                    }
                });

                if (catalog == null || catalog.Count == 0)
                    return e.Fail("catalog", refresh_proxy: !reqOk);

                return e.Success(catalog);
            });

            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            if (!similar && cache.Value.Count == 1)
                return LocalRedirect(accsArgs($"/lite/animedia?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&news={HttpUtility.UrlEncode(cache.Value[0].url)}"));

            var stpl = new SimilarTpl(cache.Value.Count);

            foreach (var res in cache.Value)
            {
                stpl.Append(
                    res.title,
                    string.Empty,
                    string.Empty,
                    $"{host}/lite/animedia?title={HttpUtility.UrlEncode(title)}&news={HttpUtility.UrlEncode(res.url)}",
                    PosterApi.Size(res.img)
                );
            }

            return ContentTpl(stpl);
            #endregion
        }
        else
        {
            #region Серии
            var cache = await InvokeCacheResult<List<(int episode, string s, string vod)>>($"animedia:{news}", TimeSpan.FromHours(1), async e =>
            {
                List<(int episode, string s, string vod)> links = null;

                await httpHydra.GetSpan($"{init.host}/{news}", html =>
                {
                    var rx = Rx.Matches("data-vid=\"([0-9]+)\"[\t ]+data-vlnk=\"([^\"]+)\"", html);
                    if (rx.Count == 0)
                        return;

                    links = new List<(int episode, string s, string vod)>(rx.Count);

                    ReadOnlySpan<char> pmovie = Rx.Slice(html, "class=\"pmovie__main-info ws-nowrap\">", "<");
                    string s = Rx.Match(pmovie, "Season[\t ]+([0-9]+)", 1, RegexOptions.IgnoreCase);
                    if (string.IsNullOrEmpty(s))
                        s = "1";

                    foreach (var row in rx.Rows())
                    {
                        var g = row.Groups();

                        string vod = g[2].Value;
                        if (!string.IsNullOrEmpty(g[1].Value) && !string.IsNullOrEmpty(vod) && vod.Contains("/vod/"))
                        {
                            if (int.TryParse(g[1].Value, out int episode) && episode > 0)
                            {
                                if (links.FirstOrDefault(i => i.episode == episode).vod == null)
                                    links.Add((episode, s, vod));
                            }
                        }
                    }
                });

                if (links == null || links.Count == 0)
                    return e.Fail("links", refresh_proxy: true);

                return e.Success(links.OrderBy(i => i.episode).ToList());
            });

            return ContentTpl(cache, () =>
            {
                var etpl = new EpisodeTpl(cache.Value.Count);

                foreach (var l in cache.Value)
                {
                    etpl.Append(
                        $"{l.episode} серия",
                        title,
                        l.s,
                        l.episode.ToString(),
                        accsArgs($"{host}/lite/animedia/video.m3u8?vod={HttpUtility.UrlEncode(l.vod)}"),
                        vast: init.vast
                    );
                }

                return etpl;
            });
            #endregion
        }
    }

    #region Video
    [HttpGet]
    [Route("lite/animedia/video.m3u8")]
    async public Task<ActionResult> Video(string vod)
    {
        if (await IsRequestBlocked(rch: false, rch_check: false))
            return badInitMsg;

        var cache = await InvokeCacheResult<string>($"animedia:{vod}", 120, async e =>
        {
            string hls = null;

            await httpHydra.GetSpan(vod, embed =>
            {
                hls = Rx.Match(embed, "file:([\t ]+)?\"([^\"]+)\"", 2);
            });

            if (string.IsNullOrEmpty(hls))
                return e.Fail("hls", refresh_proxy: true);

            return e.Success(hls);
        });

        if (!cache.IsSuccess)
            return OnError(cache.ErrorMsg, refresh_proxy: cache.refresh_proxy);

        return Redirect(HostStreamProxy(cache.Value));
    }
    #endregion
}

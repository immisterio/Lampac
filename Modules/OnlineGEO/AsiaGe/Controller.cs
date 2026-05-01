using Shared.Models.Base;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Shared;
using Shared.Models.Templates;
using Shared.Services;
using Shared.Services.HTML;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Attributes;
using Shared.Services.RxEnumerate;

namespace AsiaGe;

public class AsiaGeController : BaseOnlineController
{
    public AsiaGeController() : base(ModInit.conf) { }

    [HttpGet]
    [Staticache]
    [Route("lite/asiage")]
    async public Task<ActionResult> Index(long id, string title, int year, int serial = 0, string href = null, bool similar = false)
    {
        if (serial == 0)
            return OnError();

        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        #region search
        if (string.IsNullOrEmpty(href))
        {
            string searchTitle = similar ? title : null;

            if (searchTitle == null)
            {
                var tmdbName = await InvokeCacheResult<string>($"asiage:tmdb:{id}", 60 * 24, async e =>
                {
                    var header = HeadersModel.Init(("lcrqpasswd", CoreInit.rootPasswd));
                    var result = await Http.Get<JObject>($"http://api.themoviedb.org/3/tv/{id}?api_key={CoreInit.conf.cub.api_key}&language=en", timeoutSeconds: 5, headers: header);
                    if (result != null)
                        return e.Success(result.Value<string>("name"));

                    return e.Fail("tmdb");
                });

                searchTitle = tmdbName.Value;
            }

            if (string.IsNullOrWhiteSpace(searchTitle))
                return OnError();

            rhubSearchFallback:

            var search = await InvokeCacheResult<EmbedModel>($"asiage:search:{searchTitle}:{year}", TimeSpan.FromHours(4), async e =>
            {
                var similars = new SimilarTpl();

                await httpHydra.GetSpan($"{init.host}/index.php?do=search&subaction=search&search_start=0&full_search=0&story={HttpUtility.UrlEncode(searchTitle)}", html =>
                {
                    foreach (ReadOnlySpan<char> row in HtmlSpan.Nodes(html, "div", "class", "movie-item", HtmlSpanTargetType.Exact))
                    {
                        string link = Rx.Match(row, "href=\"https?://[^/]+/([^\"]+\\.html)\"");
                        string name = Rx.Match(row, "<div class=\"mob-titl-3\">([^<]+)</div>");

                        if (string.IsNullOrEmpty(link) || string.IsNullOrEmpty(name))
                            continue;

                        string _y = Rx.Match(row, "/year/([0-9]+)/\"") ?? string.Empty;
                        similars.Append(name, _y, string.Empty, $"{host}/lite/asiage?title={HttpUtility.UrlEncode(title)}&year={year}&serial={serial}&href={HttpUtility.UrlEncode(link)}");
                    }
                });

                if (similars.Length == 0)
                    return e.Fail("search", refresh_proxy: true);

                return e.Success(new EmbedModel() { similar = similars });
            });

            if (IsRhubFallback(search))
                goto rhubSearchFallback;

            if (!search.IsSuccess)
                return OnError(search.ErrorMsg);

            if (string.IsNullOrWhiteSpace(href))
                return ContentTpl(search.Value.similar);
        }
    #endregion

    rhubFallback:

        var cache = await InvokeCacheResult<List<(string title, string file)>>($"asiage:view:{href}", 40, async e =>
        {
            List<(string title, string file)> episodes = null;

            await httpHydra.GetSpan($"{init.host}/{href}", html =>
            {
                string fileJson = Rx.Match(html, @"file\s*:\s*(\[[\s\S]*?\])\s*,\s*(?:""vast""|vast)\s*:");
                if (fileJson == null)
                    return;

                fileJson = Regex.Replace(fileJson, ",\\s*([}\\]])", "$1");
                var files = JArray.Parse(fileJson);

                episodes = new List<(string title, string file)>();
                foreach (var item in files)
                {
                    string epTitle = item.Value<string>("title");
                    string epFile = item.Value<string>("file");

                    if (!string.IsNullOrWhiteSpace(epTitle) && !string.IsNullOrWhiteSpace(epFile))
                        episodes.Add((epTitle, epFile));
                }
            });

            if (episodes == null || episodes.Count == 0)
                return e.Fail("episodes", refresh_proxy: true);

            return e.Success(episodes);
        });

        if (IsRhubFallback(cache))
            goto rhubFallback;

        return ContentTpl(cache, () =>
        {
            var etpl = new EpisodeTpl();

            int epIndex = 1;
            foreach (var episode in cache.Value)
            {
                foreach (Match lm in Regex.Matches(episode.file, "\\{([^}]+)\\}([^;,]+)", RegexOptions.IgnoreCase))
                {
                    string link = HostStreamProxy(lm.Groups[2].Value.Trim());
                    string epNum = Regex.Match(episode.title, "([0-9]+)").Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(epNum))
                        epNum = epIndex.ToString();

                    etpl.Append(
                        episode.title,
                        title,
                        "1",
                        epNum,
                        link,
                        vast: init.vast
                    );
                }

                epIndex++;
            }

            return etpl;
        });
    }
}

using Microsoft.AspNetCore.Mvc;
using Shared.Attributes;
using Shared.Services.RxEnumerate;
using System.Text.Json;
using Shared;
using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Dreamerscast;

public class DreamerscastController : BaseOnlineController
{
    public DreamerscastController() : base(ModInit.conf) { }

    [HttpGet]
    [Staticache]
    [Route("lite/dreamerscast")]
    async public Task<ActionResult> Index(string title, int year, string uri, int s = 1, bool rjson = false, bool similar = false)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        if (string.IsNullOrWhiteSpace(uri))
        {
            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            rhubFallback:
            var cache = await InvokeCacheResult<List<SearchItem>>($"dreamerscast:search:{title}", TimeSpan.FromHours(4), textJson: true, onget: async e =>
            {
                var search = await httpHydra.Post<SearchResponse>($"{init.host}/", $"search={HttpUtility.UrlEncode(title)}&status=&pageSize=16&pageNumber=1", textJson: true, addheaders: HeadersModel.Init(
                    ("x-requested-with", "XMLHttpRequest"),
                    ("referer", init.host + "/")
                ));

                if (search?.releases == null)
                    return e.Fail("search", refresh_proxy: true);

                var catalog = new List<SearchItem>();

                foreach (var item in search.releases)
                {
                    string img = item.image;

                    if (!string.IsNullOrEmpty(img) && img.StartsWith("//"))
                        img = "https:" + img;
                    else if (!string.IsNullOrEmpty(img) && img.StartsWith("/"))
                        img = init.host + img;

                    string animeSeason = Regex.Match(item.original ?? string.Empty, " ([0-9]+)nd ").Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(animeSeason))
                        animeSeason = "1";

                    catalog.Add(new SearchItem
                    {
                        title = item.russian ?? item.original,
                        year = item.dateissue,
                        uri = item.url,
                        s = animeSeason,
                        img = img
                    });
                }

                if (catalog.Count == 0)
                    return e.Fail("catalog");

                return e.Success(catalog);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            if (!similar && cache.Value != null && cache.Value.Count == 1)
                return LocalRedirect(accsArgs($"/lite/dreamerscast?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(cache.Value[0].uri)}&s={cache.Value[0].s}"));

            return ContentTpl(cache, () =>
            {
                var stpl = new SimilarTpl(cache.Value.Count);

                foreach (var res in cache.Value)
                {
                    stpl.Append(
                        res.title,
                        res.year.ToString(),
                        string.Empty,
                        $"{host}/lite/dreamerscast?title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(res.uri)}&s={res.s}",
                        PosterApi.Size(res.img)
                    );
                }

                return stpl;
            });
        }
        else
        {
        rhubFallback:
            var cache = await InvokeCacheResult<List<Episode>>($"dreamerscast:release:{uri}", 20, textJson: true, onget: async e =>
            {
                var episodes = new List<Episode>();

                await httpHydra.GetSpan(init.host + uri, html =>
                {
                    string base64 = Rx.Slice(html, "Playerjs(\"#2", "\");").ToString();
                    string json = CrypTo.DecodeBase64(Regex.Replace(base64, "//[^=]+==", ""));
                    if (string.IsNullOrEmpty(json))
                        return;

                    try
                    {
                        var root = JsonSerializer.Deserialize<PlayerRoot>(json, new JsonSerializerOptions
                        {
                            AllowTrailingCommas = true
                        });

                        foreach (var item in root.file)
                        {
                            string hls = getHls(item.file);
                            if (hls == null)
                                continue;

                            episodes.Add(new Episode
                            {
                                name = item.title,
                                episode = Regex.Match(item.title ?? string.Empty, "([0-9]+)").Groups[1].Value,
                                hls = hls
                            });
                        }
                    }
                    catch { }
                });

                if (episodes.Count == 0)
                    return e.Fail("episodes", refresh_proxy: true);

                return e.Success(episodes);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return ContentTpl(cache, () =>
            {
                string season = s.ToString();
                var etpl = new EpisodeTpl(cache.Value.Count);

                foreach (var item in cache.Value)
                {
                    string ep = string.IsNullOrWhiteSpace(item.episode) ? "1" : item.episode;
                    string nm = string.IsNullOrWhiteSpace(item.name) ? $"{ep} серия" : item.name;

                    etpl.Append(
                        nm,
                        title,
                        season,
                        ep,
                        HostStreamProxy(item.hls)
                    );
                }

                return etpl;
            });
        }
    }


    static string getHls(string file)
    {
        if (string.IsNullOrEmpty(file))
            return null;

        foreach (Match match in Regex.Matches(file, "https?://[^\\s]+"))
        {
            string url = match.Value;
            if (url.Contains("/hls/"))
                return url.Trim().TrimEnd(',', ';');
        }

        return null;
    }
}

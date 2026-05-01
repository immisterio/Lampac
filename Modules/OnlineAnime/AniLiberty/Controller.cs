using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.Services;
using Shared.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace AniLiberty;

public class AniLibertyController : BaseOnlineController
{
    static readonly HttpClient http2Client = FriendlyHttp.CreateHttp2Client();

    public AniLibertyController() : base(ModInit.conf)
    {
        requestInitialization += () =>
        {
            if (init.httpversion == 2)
                httpHydra.RegisterHttp(http2Client);
        };
    }

    [HttpGet]
    [Staticache]
    [Route("lite/aniliberty")]
    async public Task<ActionResult> Index(string title, int year, string releases, bool rjson = false, bool similar = false, string source = null, string id = null)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        if (string.IsNullOrEmpty(releases) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
        {
            if (source.Equals("aniLiberty", StringComparison.OrdinalIgnoreCase))
                releases = id;
        }

        if (string.IsNullOrEmpty(releases))
        {
            #region Поиск
            string stitle = StringConvert.SearchName(title);
            if (string.IsNullOrEmpty(stitle))
                return OnError();

            rhubFallback:
            var cache = await InvokeCacheResult<List<(string title, string year, int releases, string cover)>>($"aniliberty:search:{title}:{similar}", TimeSpan.FromHours(4), async e =>
            {
                var search = await httpHydra.Get<List<SearchItem>>($"{init.host}/api/v1/app/search/releases?query={HttpUtility.UrlEncode(title)}");

                if (search == null || search.Count == 0)
                    return e.Fail("search");

                bool checkName = true;
                var catalog = new List<(string title, string year, int releases, string cover)>(search.Count);

            retry: foreach (var anime in search)
                {
                    string name_main = StringConvert.SearchName(anime.name?.main);
                    string name_english = StringConvert.SearchName(anime.name?.english);

                    if (!checkName || similar || (name_main != null && name_main.StartsWith(stitle)) || (name_english != null && name_english.StartsWith(stitle)))
                    {
                        string img = null;
                        var cover = anime.poster;
                        if (cover != null)
                            img = init.host + cover.src;

                        catalog.Add(($"{anime.name?.main} / {anime.name?.english}", anime.year.ToString(), anime.id, img));
                    }
                }

                if (catalog.Count == 0)
                {
                    if (checkName && similar == false)
                    {
                        checkName = false;
                        goto retry;
                    }

                    return e.Fail("catalog");
                }

                return e.Success(catalog);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            if (!similar && cache.Value != null && cache.Value.Count == 1)
                return LocalRedirect(accsArgs($"/lite/aniliberty?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&releases={cache.Value.First().releases}"));

            return ContentTpl(cache, () =>
            {
                var stpl = new SimilarTpl(cache.Value.Count);

                foreach (var res in cache.Value)
                {
                    stpl.Append(
                        res.title,
                        res.year,
                        string.Empty,
                        $"{host}/lite/aniliberty?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&releases={res.releases}",
                        PosterApi.Size(res.cover)
                    );
                }

                return stpl;

            });
            #endregion
        }
        else
        {
        #region Серии
        rhubFallback:
            var cache = await InvokeCacheResult<Release>($"aniliberty:releases:{releases}", 20, async e =>
            {
                var root = await httpHydra.Get<Release>($"{init.host}/api/v1/anime/releases/{releases}");

                if (root?.episodes == null)
                    return e.Fail("episodes");

                return e.Success(root);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return ContentTpl(cache, () =>
            {
                var episodes = cache.Value.episodes;
                var etpl = new EpisodeTpl(episodes.Length);

                foreach (var episode in episodes)
                {
                    string alias = cache.Value.alias ?? "";
                    string season = Regex.Match(alias, "-([0-9]+)(nd|th)").Groups[1].Value;
                    if (string.IsNullOrEmpty(season))
                    {
                        season = Regex.Match(alias, "season-([0-9]+)").Groups[1].Value;
                        if (string.IsNullOrEmpty(season))
                            season = "1";
                    }

                    string number = episode.ordinal;

                    string name = episode.name;
                    name = string.IsNullOrEmpty(name) ? $"{number} серия" : name;

                    var streams = new StreamQualityTpl();
                    foreach (var f in new List<(string quality, string url)>
                    {
                        ("1080p", episode.hls_1080),
                        ("720p", episode.hls_720),
                        ("480p", episode.hls_480)
                    })
                    {
                        if (string.IsNullOrEmpty(f.url))
                            continue;

                        streams.Append(HostStreamProxy(f.url), f.quality);
                    }

                    var first = streams.Firts();
                    if (first != null)
                    {
                        etpl.Append(
                            name,
                            title,
                            season,
                            number,
                            first.link,
                            streamquality: streams
                        );
                    }
                }

                return etpl;
            });
            #endregion
        }
    }
}

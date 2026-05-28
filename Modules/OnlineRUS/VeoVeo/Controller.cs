using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.Services;
using Shared.Services.RxEnumerate;
using Shared.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace VeoVeo;

public class VeoVeoController : BaseOnlineController
{
    static readonly HttpClient http2Client = FriendlyHttp.CreateHttp2Client();

    public VeoVeoController() : base(ModInit.conf)
    {
        requestInitialization += () =>
        {
            if (init.httpversion == 2)
                httpHydra.RegisterHttp(http2Client);
        };
    }

    [HttpGet, Staticache(manually: true)]
    [Route("lite/veoveo")]
    async public Task<ActionResult> Index(long movieid, string imdb_id, long kinopoisk_id, string title, string original_title, byte clarification, short s = -1, bool rjson = false, bool similar = false)
    {
        if (await IsRequestBlocked(rch: true, rch_check: !similar))
            return badInitMsg;

        #region search
        if (movieid == 0)
        {
            if (similar)
                return await Spider(title);

            var movie = await search(imdb_id, kinopoisk_id, title, original_title);
            if (movie == null)
                return await Spider(clarification == 1 ? title : (original_title ?? title));

            movieid = movie.id;
        }
        #endregion

        #region media
    rhubFallback:

        var cache = await InvokeCacheResult<List<CatalogItem>>($"{init.plugin}:view:{movieid}", 20, async e =>
        {
            var root = await httpHydra.Get<List<CatalogItem>>($"{init.host}/balancer-api/proxy/playlists/catalog-api/episodes?content-id={movieid}");

            if (root == null || root.Count == 0)
                return e.Fail("data");

            return e.Success(root);
        });

        if (IsRhubFallback(cache))
            goto rhubFallback;
        #endregion

        return ContentTpl(cache, () =>
        {
            var firstCatalogItem = cache.Value.First();

            if (firstCatalogItem.season?.order == 0)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, 1);

                if (firstCatalogItem != null)
                {
                    var episodes = firstCatalogItem.episodeVariants;
                    if (episodes != null)
                    {
                        foreach (var episode in episodes)
                        {
                            string file = episode?.filepath;
                            if (!string.IsNullOrWhiteSpace(file))
                            {
                                string stream = file.Contains(".json")
                                    ? accsArgs($"{host}/lite/veoveo/parsed.m3u8?link={EncryptQuery(file)}")
                                    : HostStreamProxy(file);

                                mtpl.Append(
                                    episode.title ?? "1080p",
                                    stream,
                                    vast: init.vast
                                );
                            }
                        }
                    }
                }

                return mtpl;
                #endregion
            }
            else
            {
                #region Сериал
                if (s == -1)
                {
                    var tpl = new SeasonTpl();
                    var hash = new HashSet<int>();
                    string enc_title = HttpUtility.UrlEncode(title);
                    string enc_original_title = HttpUtility.UrlEncode(original_title);

                    foreach (var item in cache.Value)
                    {
                        int season = item.season?.order ?? 0;
                        if (hash.Add(season))
                        {
                            tpl.Append(
                                $"{season} сезон",
                                $"{host}/lite/veoveo?rjson={rjson}&movieid={movieid}&kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&title={enc_title}&original_title={enc_original_title}&s={season}",
                                season
                            );
                        }
                    }

                    return tpl;
                }
                else
                {
                    var etpl = new EpisodeTpl();

                    foreach (var episode in cache.Value
                        .Where(i => (i.season?.order ?? 0) == s)
                        .OrderBy(i => i.order))
                    {
                        string name = episode.title;

                        var variants = episode.episodeVariants;
                        var fileToken = variants?
                            .OrderByDescending(i => (i.filepath ?? "").Contains(".m3u8"))
                            .FirstOrDefault();

                        string file = fileToken?.filepath;
                        if (!string.IsNullOrWhiteSpace(file))
                        {
                            string stream = HostStreamProxy(file);
                            if (stream.Contains(".json"))
                                stream = accsArgs($"{host}/lite/veoveo/parsed.m3u8?link={EncryptQuery(file)}");

                            etpl.Append(
                                name ?? $"{episode.order} серия",
                                title ?? original_title,
                                s,
                                episode.order,
                                stream,
                                vast: init.vast
                            );
                        }
                    }

                    return etpl;
                }
                #endregion
            }
        });
    }

    #region Parsed
    [HttpGet]
    [Route("lite/veoveo/parsed.m3u8")]
    async public Task<ActionResult> Parsed(string link)
    {
        link = DecryptQuery(link);
        if (string.IsNullOrWhiteSpace(link))
            return OnError();

        string m3u8 = await InvokeCache($"veoveo:parsed:{link}", 20, async () =>
        {
            var parsed = await httpHydra.Get<ParsedResponse>(link);
            if (parsed?.sources != null && parsed.sources.Count > 0)
            {
                string m3u8 = parsed.sources.FirstOrDefault()?.link;
                if (!string.IsNullOrEmpty(m3u8))
                    return m3u8;
            }

            return null;
        });

        if (!string.IsNullOrEmpty(m3u8))
            return Redirect(HostStreamProxy(m3u8));

        return OnError();
    }
    #endregion

    #region Spider
    [HttpGet, Staticache(manually: true)]
    [Route("lite/veoveo-spider")]
    async public Task<ActionResult> Spider(string title)
    {
        string stitle = SearchNameTo.Convert(title);
        if (stitle == null)
            return OnError();

        var stpl = new SimilarTpl(100);

        foreach (var m in ModInit.database)
        {
            if (stpl.data.Count >= 100)
                break;

            if (SearchNameTo.Contains(m.title, stitle) ||
                SearchNameTo.Contains(m.originalTitle, stitle))
            {
                stpl.Append(
                    m.title ?? m.originalTitle,
                    m.year.ToString(),
                    string.Empty,
                    $"{host}/lite/veoveo?movieid={m.id}",
                    PosterApi.Find(m.kinopoiskId, m.imdbId)
                );
            }
        }

        return ContentTpl(stpl);
    }
    #endregion


    #region search
    ValueTask<Movie> search(string imdb_id, long kinopoisk_id, string title, string original_title)
    {
        if (!string.IsNullOrEmpty(init.token) && (!string.IsNullOrEmpty(imdb_id) || kinopoisk_id > 0))
            return searchApi(imdb_id, kinopoisk_id);

        string stitle = SearchNameTo.Convert(title);
        string sorigtitle = SearchNameTo.Convert(original_title);

        if (ModInit.databaseById != null)
        {
            foreach (var key in new[]
            {
                kinopoisk_id > 0 ? kinopoisk_id.ToString() : null,
                imdb_id,
                sorigtitle,
                stitle
            })
            {
                if (!string.IsNullOrEmpty(key) && ModInit.databaseById.TryGetValue(key, out var item))
                    return ValueTask.FromResult(item);
            }

            return default;
        }
        else
        {
            Movie goSearch(bool searchToId)
            {
                if (searchToId && kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id))
                    return null;

                // уже был поиск в api
                if (searchToId && !string.IsNullOrEmpty(init.token))
                    return null;

                foreach (var item in ModInit.database)
                {
                    if (searchToId)
                    {
                        if (kinopoisk_id > 0)
                        {
                            if (item.kinopoiskId == kinopoisk_id)
                                return item;
                        }

                        if (!string.IsNullOrEmpty(imdb_id))
                        {
                            if (item.imdbId == imdb_id)
                                return item;
                        }
                    }
                    else
                    {
                        if (SearchNameTo.Equals(item.originalTitle, sorigtitle) ||
                            SearchNameTo.Equals(item.title, stitle))
                            return item;
                    }
                }

                return null;
            }

            return ValueTask.FromResult(goSearch(true) ?? goSearch(false));
        }
    }
    #endregion

    #region searchApi
    ValueTask<Movie> searchApi(string imdb_id, long kinopoisk_id)
    {
        return InvokeCache($"veoveo:searchApi:{imdb_id}:{kinopoisk_id}", TimeSpan.FromHours(4), async () =>
        {
            async Task<Movie> MOVIE_ID(string url)
            {
                string MOVIE_ID = null;
                await httpHydra.GetSpan(url, html =>
                {
                    MOVIE_ID = Rx.Match(html, "window.MOVIE_ID=([0-9]+);");
                });

                if (MOVIE_ID != null && int.TryParse(MOVIE_ID, out int _id) && _id > 0)
                    return new Movie() { id = _id };

                return null;
            }

            Movie movie = null;

            if (kinopoisk_id > 0)
                movie = await MOVIE_ID($"{init.host}/balancer-api/iframe?kp={kinopoisk_id}&token={init.token}");

            if (!string.IsNullOrEmpty(imdb_id) && movie == null)
                movie = await MOVIE_ID($"{init.host}/balancer-api/iframe?imdb={imdb_id}&token={init.token}");

            return movie;
        });
    }
    #endregion
}

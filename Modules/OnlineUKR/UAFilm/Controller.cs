using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace UAFilm;

public class ApiController : BaseOnlineController
{
    public ApiController() : base(ModInit.conf) { }

    [HttpGet]
    [Route("lite/uafilm")]
    async public Task<ActionResult> Index(long id, int orid, string imdb_id, long kinopoisk_id, string title, string original_title, int year, int s = -1, bool rjson = false, bool similar = false)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        string defaultargs = $"&orid={orid}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&year={year}";

        #region search
        if (orid == 0)
        {
        rhubSearchFallback:

            var search = await InvokeCacheResult<List<Result>>($"uafilm:search:{id}:{title}:{original_title}:{year}", TimeSpan.FromHours(4), async e =>
            {
                string qname = similar ? title : (original_title ?? title);
                var root = await httpHydra.Get<SearchRoot>($"{init.host}/api/v1/search/{HttpUtility.UrlEncode(qname)}?loader=searchPage", IgnoreDeserializeObject: true);

                if (root?.results == null || root.results.Count == 0)
                    return e.Fail("search results", refresh_proxy: true);

                return e.Success(root.results);
            });

            if (IsRhubFallback(search))
                goto rhubSearchFallback;

            if (!search.IsSuccess)
                return OnError(search.ErrorMsg);

            var stpl = new SimilarTpl(search.Value.Count);
            string sorigtitle = StringConvert.SearchName(original_title);

            foreach (var res in search.Value)
            {
                if (similar == false)
                {
                    if (res.imdb_id == imdb_id ||
                        res.tmdb_id == id ||
                        (StringConvert.SearchName(res.original_title) == sorigtitle && res.year == year))
                    {
                        orid = res.id;
                        break;
                    }
                }
                stpl.Append(
                    res.name,
                    res.year.ToString(),
                    res.original_title,
                    $"{host}/lite/uafilm?orid={res.id}{defaultargs}",
                    PosterApi.Size(res.poster)
                );
            }

            if (orid == 0)
                return ContentTpl(stpl);
        }
    #endregion

    #region cache
    rhubFallback:

        var cache = await InvokeCacheResult<ItemRoot>($"uafilm:{orid}:{(s > 0 ? s : 1)}", TimeSpan.FromHours(1), async e =>
        {
            string uri = s > 0
                ? $"{init.host}/api/v1/titles/{orid}/seasons/{s}?loader=seasonPage"
                : $"{init.host}/api/v1/titles/{orid}?loader=titlePage";

            var root = await httpHydra.Get<ItemRoot>(uri, IgnoreDeserializeObject: true);

            if (root?.episodes?.data == null && root?.title?.videos == null)
                return e.Fail("root", refresh_proxy: true);

            return e.Success(root);
        });

        if (IsRhubFallback(cache))
            goto rhubFallback;

        if (!cache.IsSuccess)
            return OnError(cache.ErrorMsg);
        #endregion

        if (cache.Value.title.is_series)
        {
            #region Сериал
            if (cache.Value.episodes?.data == null || cache.Value.episodes.data.Count == 0)
                return OnError("episodes");

            var episodes = cache.Value.episodes.data;

            if (s == -1)
            {
                var tpl = new SeasonTpl();

                for (int i = 1; i <= cache.Value.title.seasons_count; i++)
                {
                    tpl.Append(
                        $"{i} сезон",
                        $"{host}/lite/uafilm?rjson={rjson}&s={i}{defaultargs}",
                        i.ToString()
                    );
                }

                return ContentTpl(tpl);
            }
            else
            {
                var etpl = new EpisodeTpl();
                string sArhc = s.ToString();

                foreach (var episode in episodes.Where(i => i.season_number == s && i.primary_video != null))
                {
                    string episodeNum = episode.episode_number.ToString();
                    string link = $"{host}/lite/uafilm/video.m3u8?id={episode.primary_video.id}";

                    etpl.Append(
                        $"{episodeNum} серия",
                        episode.primary_video.name ?? title ?? original_title,
                        sArhc,
                        episodeNum,
                        accsArgs(link)
                    );
                }

                return ContentTpl(etpl);
            }
            #endregion
        }
        else
        {
            #region Фильм
            var mtpl = new MovieTpl(title, original_title);

            foreach (var video in cache.Value.title.videos)
            {
                if (video.origin is "ashdi" or "tortuga" or "hdvbua")
                    mtpl.Append(video.name ?? title, HostStreamProxy(video.src));
            }

            return ContentTpl(mtpl);
            #endregion
        }
    }

    #region Video
    [HttpGet]
    [Route("lite/uafilm/video.m3u8")]
    async public Task<ActionResult> Video(int id)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        var cache = await InvokeCacheResult<Video>($"uafilm:video:{id}", TimeSpan.FromHours(1), async e =>
        {
            var root = await httpHydra.Get<WatchRoot>($"{init.host}/api/v1/watch/{id}");
            if (string.IsNullOrEmpty(root?.video?.src))
                return e.Fail("video", refresh_proxy: true);

            return e.Success(root.video);
        });

        if (!cache.IsSuccess)
            return OnError(cache.ErrorMsg);

        return Redirect(HostStreamProxy(cache.Value.src));
    }
    #endregion
}

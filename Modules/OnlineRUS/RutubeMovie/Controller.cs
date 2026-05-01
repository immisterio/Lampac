using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Models.Templates;
using Shared.Services;
using Shared.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace RutubeMovie;

public class RutubeMovieController : BaseOnlineController
{
    private static readonly HttpClient httpClient = FriendlyHttp.CreateHttpClient();

    public RutubeMovieController() : base(ModInit.conf)
    {
        requestInitialization += () =>
        {
            if (init.httpversion == 1)
                httpHydra.RegisterHttp(httpClient);
        };
    }

    [HttpGet]
    [Route("lite/rutubemovie")]
    public async Task<ActionResult> Index(string title, string original_title, int year, int serial)
    {
        string searchTitle = StringConvert.SearchName(title);
        if (string.IsNullOrEmpty(searchTitle) || year == 0 || serial == 1)
            return OnError();

        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        rhubFallback:
        string memKey = $"rutubemovie:view:{searchTitle}:{year}:{(rch?.enable == true ? requestInfo.Country : "")}";

        var cache = await InvokeCacheResult<List<Result>>(memKey, TimeSpan.FromHours(4), textJson: true, onget: async e =>
        {
            string uri = $"api/search/video/?content_type=video&duration=movie&query={HttpUtility.UrlEncode($"{title} {year}")}";

            var root = await httpHydra.Get<RootSearch>($"{init.host}/{uri}", textJson: true);

            if (root?.results == null || root.results.Count == 0)
                return e.Fail("content", refresh_proxy: true);

            return e.Success(root.results);
        });

        if (IsRhubFallback(cache))
            goto rhubFallback;

        return ContentTpl(cache, () =>
        {
            var mtpl = new MovieTpl(title, original_title, cache.Value.Count);

            foreach (var movie in cache.Value)
            {
                string name = StringConvert.SearchName(movie.title);
                if (name != null && name.Contains(searchTitle) && (name.Contains(year.ToString()) || name.Contains((year + 1).ToString()) || name.Contains((year - 1).ToString())))
                {
                    long duration = movie.duration;
                    if (duration > 3000)
                    {
                        if (name.Contains("трейлер") || name.Contains("trailer") ||
                            name.Contains("премьера") || name.Contains("обзор") ||
                            name.Contains("сезон") || name.Contains("сериал") ||
                            name.Contains("серия") || name.Contains("серий"))
                            continue;

                        if (movie.category.id == 4)
                        {
                            if (movie.is_hidden || movie.is_deleted || movie.is_adult || movie.is_locked || movie.is_audio || movie.is_paid || movie.is_livestream)
                                continue;

                            mtpl.Append(
                                movie.title,
                                $"{host}/lite/rutubemovie/play?linkid={movie.id}",
                                "call",
                                vast: init.vast
                            );
                        }
                    }
                }
            }

            return mtpl;
        });
    }

    [HttpGet]
    [Route("lite/rutubemovie/play")]
    public async Task<ActionResult> Movie(string linkid)
    {
        if (string.IsNullOrEmpty(linkid))
            return OnError();

        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        rhubFallback:
        var cache = await InvokeCacheResult<string>($"rutubemovie:play:{linkid}", 20, async e =>
        {
            var root = await httpHydra.Get<RootPlayOptions>($"{init.host}/api/play/options/{linkid}/?no_404=true&referer=&pver=v2&client=wdp");

            if (string.IsNullOrEmpty(root?.video_balancer?.m3u8))
                return e.Fail("video_balancer", refresh_proxy: true);

            return e.Success(root.video_balancer.m3u8);
        });

        if (IsRhubFallback(cache))
            goto rhubFallback;

        return ContentTo(VideoTpl.ToJson(
            "play",
            HostStreamProxy(cache.Value),
            "auto",
            vast: init.vast,
            httpContext: HttpContext
        ));
    }
}

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.RutubeMovie;

namespace Online.Controllers
{
    public class RutubeMovie : BaseOnlineController
    {
        public RutubeMovie() : base(AppInit.conf.RutubeMovie) { }

        [HttpGet]
        [Route("lite/rutubemovie")]
        async public Task<ActionResult> Index(string title, string original_title, int year, int serial)
        {
            string searchTitle = StringConvert.SearchName(title);
            if (string.IsNullOrEmpty(searchTitle) || year == 0 || serial == 1)
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            string memKey = $"rutubemovie:view:{searchTitle}:{year}:{(rch?.enable == true ? requestInfo.Country : "")}";
            var cache = await InvokeCacheResult<Result[]>(memKey, 40, async e =>
            {
                string uri = $"api/search/video/?content_type=video&duration=movie&query={HttpUtility.UrlEncode($"{title} {year}")}";

                var root = await httpHydra.Get<JObject>($"{init.host}/{uri}");

                if (root == null || !root.ContainsKey("results"))
                    return e.Fail("content", refresh_proxy: true);

                return e.Success(root["results"].ToObject<Result[]>());
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return await ContentTpl(cache, () =>
            {
                var mtpl = new MovieTpl(title, original_title, cache.Value.Length);

                foreach (var movie in cache.Value)
                {
                    string name = StringConvert.SearchName(movie.title);
                    if (name != null && name.Contains(searchTitle) && (name.Contains(year.ToString()) || name.Contains((year + 1).ToString()) || name.Contains((year - 1).ToString())))
                    {
                        long duration = movie.duration;
                        if (duration > 3000) // 50 minutes
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

                                mtpl.Append(movie.title, $"{host}/lite/rutubemovie/play?linkid={movie.id}", "call", vast: init.vast);
                            }
                        }
                    }
                }

                return mtpl;
            });
        }


        [HttpGet]
        [Route("lite/rutubemovie/play")]
        async public ValueTask<ActionResult> Movie(string linkid)
        {
            if (string.IsNullOrEmpty(linkid))
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<string>($"rutubemovie:play:{linkid}", 20, async e =>
            {
                var root = await httpHydra.Get<JObject>($"{init.host}/api/play/options/{linkid}/?no_404=true&referer=&pver=v2&client=wdp");

                if (root == null || !root.ContainsKey("video_balancer"))
                    return e.Fail("video_balancer", refresh_proxy: true);

                return e.Success(root["video_balancer"].Value<string>("m3u8"));
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return ContentTo(VideoTpl.ToJson("play", HostStreamProxy(cache.Value), "auto", vast: init.vast));
        }
    }
}

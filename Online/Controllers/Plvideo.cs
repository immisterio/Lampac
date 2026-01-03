using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Plvideo;

namespace Online.Controllers
{
    public class Plvideo : BaseOnlineController
    {
        public Plvideo() : base(AppInit.conf.Plvideo) { }

        [HttpGet]
        [Route("lite/plvideo")]
        async public Task<ActionResult> Index(string title, string original_title, int year, int serial, bool rjson = false)
        {
            string searchTitle = StringConvert.SearchName(title);
            if (string.IsNullOrEmpty(searchTitle) || year == 0 || serial == 1)
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<Item[]>($"plvideo:view:{searchTitle}:{year}", 40, async e =>
            {
                var root = await httpHydra.Get<JObject>($"{init.host}/v1/videos?Type=video&Query={HttpUtility.UrlEncode($"{title} {year}")}&From=0&Size=20&Aud=16&Qf=false");

                if (root == null || !root.ContainsKey("items"))
                    return e.Fail("content", refresh_proxy: true);

                return e.Success(root["items"].ToObject<Item[]>());
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return await ContentTpl(cache, () =>
            {
                var mtpl = new MovieTpl(title, original_title, cache.Value.Length);

                foreach (var movie in cache.Value)
                {
                    string name = StringConvert.SearchName(movie.title);
                    if (name != null && name.StartsWith(searchTitle) && (name.Contains(year.ToString()) || name.Contains((year + 1).ToString()) || name.Contains((year - 1).ToString())))
                    {
                        long duration = movie.uploadFile.videoDuration;
                        if (duration > 1900000) // 30 minutes
                        {
                            if (name.Contains("трейлер") || name.Contains("премьера") || name.Contains("сезон") || name.Contains("сериал") || name.Contains("серия") || name.Contains("серий"))
                                continue;

                            if (movie.visible != "public")
                                continue;

                            mtpl.Append(movie.title, $"{host}/lite/plvideo/movie?linkid={movie.id}", "call");
                        }
                    }
                }

                return mtpl;
            });
        }


        [HttpGet]
        [Route("lite/plvideo/movie")]
        async public ValueTask<ActionResult> Movie(string linkid)
        {
            if (string.IsNullOrEmpty(linkid))
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<Dictionary<string, Profile>>($"plvideo:play:{linkid}", 20, async e =>
            {
                var root = await httpHydra.Get<JObject>($"{init.host}/v1/videos/{linkid}?Aud=16");

                if (root == null || !root.ContainsKey("item"))
                    return e.Fail("item", refresh_proxy: true);

                return e.Success(root["item"]["profiles"].ToObject<Dictionary<string, Profile>>());
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            var streams = new StreamQualityTpl();
            foreach (string q in new string[] { "2160p", "1440p", "1080p", "720p", "468p", "360p", "240p" })
            {
                if (cache.Value.TryGetValue(q, out Profile p))
                {
                    if (!string.IsNullOrEmpty(p.hls))
                        streams.Append(HostStreamProxy(p.hls + "#.m3u8"), q);
                }
            }

            return ContentTo(VideoTpl.ToJson("play", streams.Firts().link, streams.Firts().quality, streamquality: streams, vast: init.vast));
        }
    }
}

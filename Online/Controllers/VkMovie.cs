using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.VkMovie;

namespace Online.Controllers
{
    public class VkMovie : BaseOnlineController
    {
        static readonly int client_id = 52461373;
        static string access_token;
        static DateTime token_expires;

        [HttpGet]
        [Route("lite/vkmovie")]
        async public ValueTask<ActionResult> Index(string title, string original_title, int year, int serial, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.VkMovie);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (serial == 1)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web", out string rch_error))
                return ShowError(rch_error);

            if (!await EnsureAnonymToken(init, proxy))
                return ShowError("token");

            string searchTitle = StringConvert.SearchName(title);

            reset:
            var cache = await InvokeCache<CatalogVideo[]>(rch.ipkey($"vkmovie:view:{searchTitle}:{year}", proxyManager), cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string url = $"{init.host}/method/catalog.getVideoSearchWeb2?v=5.264&client_id={client_id}";
                string data = $"screen_ref=search_video_service&input_method=keyboard_search_button&q={HttpUtility.UrlEncode($"{title} {year}")}&access_token={access_token}";

                var root = rch.enable
                    ? await rch.Post<JObject>(url, data, httpHeaders(init))
                    : await Http.Post<JObject>(url, data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));

                if (root == null || !root.ContainsKey("response"))
                    return res.Fail("response");

                var videos = root["response"]?["catalog_videos"]?.ToObject<CatalogVideo[]>();
                if (videos == null)
                    return res.Fail("catalog_videos");

                return videos;
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () =>
            {
                var mtpl = new MovieTpl(title, original_title, cache.Value.Length);

                foreach (var item in cache.Value
                    .OrderByDescending(i => i.video?.files?.mp4_2160 != null)
                    .ThenByDescending(i => i.video?.files?.mp4_1440 != null)
                    .ThenByDescending(i => i.video?.files?.mp4_1080 != null))
                {
                    var video = item.video;
                    if (video == null || video.files == null)
                        continue;

                    string name = StringConvert.SearchName(video.title);
                    if (name == null || !name.Contains(searchTitle))
                        continue;

                    if (!(name.Contains(year.ToString()) || name.Contains((year + 1).ToString()) || name.Contains((year - 1).ToString())))
                        continue;

                    if (video.duration < 3000) // 50 min
                        continue;

                    if (name.Contains("трейлер") || name.Contains("trailer") || 
                        name.Contains("премьера") || name.Contains("обзор") ||
                        name.Contains("сезон") || name.Contains("сериал") ||
                        name.Contains("серия") || name.Contains("серий"))
                        continue;

                    if (string.IsNullOrEmpty(video.files.mp4_2160) 
                        && string.IsNullOrEmpty(video.files.mp4_1440) 
                        && string.IsNullOrEmpty(video.files.mp4_1080) 
                        && string.IsNullOrEmpty(video.files.mp4_720))
                        continue;

                    var streams = new StreamQualityTpl();

                    void append(string url, string quality)
                    {
                        if (!string.IsNullOrEmpty(url))
                            streams.Append(HostStreamProxy(init, url, proxy: proxy), quality);
                    }

                    //append(video.files.hls, "auto");
                    append(video.files.mp4_2160, "2160p");
                    append(video.files.mp4_1440, "1440p");
                    append(video.files.mp4_1080, "1080p");
                    append(video.files.mp4_720, "720p");
                    append(video.files.mp4_480, "480p");
                    append(video.files.mp4_360, "360p");
                    append(video.files.mp4_240, "240p");
                    append(video.files.mp4_144, "144p");

                    if (!streams.Any())
                        continue;

                    SubtitleTpl? subtitles = null;

                    if (video.subtitles != null && video.subtitles.Length > 0)
                    {
                        var subtitleTpl = new SubtitleTpl(video.subtitles.Length);

                        foreach (var subtitle in video.subtitles)
                        {
                            if (string.IsNullOrEmpty(subtitle?.url))
                                continue;

                            string label = subtitle.manifest_name;
                            if (string.IsNullOrEmpty(label))
                                label = !string.IsNullOrEmpty(subtitle.title) ? subtitle.title : subtitle.lang;

                            subtitleTpl.Append(label, HostStreamProxy(init, subtitle.url, proxy: proxy));
                        }

                        if (!subtitleTpl.IsEmpty())
                            subtitles = subtitleTpl;
                    }

                    mtpl.Append(video.title, streams.Firts().link, streamquality: streams, subtitles: subtitles, headers: HeadersModel.Init(init.headers), vast: init.vast);
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();

            }, gbcache: !rch.enable);
        }


        async ValueTask<bool> EnsureAnonymToken(BaseSettings init, System.Net.WebProxy proxy)
        {
            // If token exists and not expired - ok
            if (!string.IsNullOrEmpty(access_token) && token_expires > DateTime.UtcNow)
                return true;

            var sem = _semaphoreLocks.GetOrAdd("vkmovie:anonym_token", _ => new System.Threading.SemaphoreSlim(1, 1));
            await sem.WaitAsync();
            try
            {
                // double-check after acquiring semaphore
                if (!string.IsNullOrEmpty(access_token) && token_expires > DateTime.UtcNow)
                    return true;

                string url = "https://login.vk.com/?act=get_anonym_token";
                string postData = $"client_secret=o557NLIkAErNhakXrQ7A&client_id={client_id}&scopes=audio_anonymous%2Cvideo_anonymous%2Cphotos_anonymous%2Cprofile_anonymous&isApiOauthAnonymEnabled=false&version=1&app_id=6287487";

                JObject root = null;

                try
                {
                    root = await Http.Post<JObject>(url, postData, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                }
                catch
                {
                    // ignore
                }

                if (root == null || !root.ContainsKey("data"))
                    return false;

                var data = root["data"];

                string token = data?["access_token"]?.ToString();
                if (string.IsNullOrEmpty(token))
                    return false;

                access_token = token;

                long? expires = data?["expires"]?.ToObject<long?>()
                    ?? data?["expired_at"]?.ToObject<long?>()
                    ?? -1;

                // "expires":1760171110 (24ч) | 20ч
                token_expires = expires == -1 
                    ? DateTime.UtcNow.AddHours(10) 
                    : DateTimeOffset.FromUnixTimeSeconds(expires.Value).UtcDateTime.AddHours(-4);

                return true;
            }
            finally
            {
                sem.Release();
            }
        }
    }
}

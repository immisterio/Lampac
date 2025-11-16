using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Lumex;
using Shared.Models.Online.Settings;
using System.Net;
using System.Text;

namespace Online.Controllers
{
    public class VideoCDN : BaseOnlineController
    {
        static VideoCDN() 
        { 
            Directory.CreateDirectory("cache/logs/VideoCDN");

            Lumex.FixHostEvent();
        }

        #region Initialization
        async ValueTask<LumexSettings> Initialization()
        {
            var init = await loadKit(AppInit.conf.VideoCDN, (j, i, c) =>
            {
                if (j.ContainsKey("log"))
                    i.log = c.log;

                i.clientId = c.clientId;
                i.username = c.username;
                i.password = c.password;
                i.domain = Regex.Replace(c.domain ?? "bwa", "^https?://", "").Split(".")[0];
                i.corseu = false;
                i.rhub = !i.disable_protection;

                return i;
            });

            init.rhub = !init.disable_protection;
            return init;
        }
        #endregion

        [HttpGet]
        [Route("lite/videocdn")]
        async public ValueTask<ActionResult> Index(long content_id, string content_type, string imdb_id, long kinopoisk_id, string title, string original_title, string t, int clarification, bool similar = false, int s = -1, int serial = -1, bool rjson = false, bool checksearch = false)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.username) || string.IsNullOrEmpty(init.password))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            if (content_id == 0)
            {
                var search = await InvokeCache($"videocdn:search:{imdb_id}:{kinopoisk_id}:{title ?? original_title}:{clarification}:{similar}", TimeSpan.FromHours(1),
                    () => Search(init, imdb_id, kinopoisk_id, title, original_title, serial, clarification, similar, proxy)
                );

                if (search.content_type == null && search.similar.data == null)
                    return OnError();

                if (search.similar.data != null)
                    return ContentTo(rjson ? search.similar.ToJson() : search.similar.ToHtml());

                content_id = search.content_id;
                content_type = search.content_type;
            }

            if (content_id == 0 || string.IsNullOrEmpty(content_type))
                return OnError();

            if (checksearch)
                return Content("data-json=");

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: serial == 0 ? null : -1);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            string accessToken = await getToken(proxy);
            if (string.IsNullOrEmpty(accessToken))
                return OnError();

            var player = await getPlayer(content_id, content_type, accessToken, proxy);
            if (player == null)
                return OnError();

            if (player.content_type is "movie" or "anime")
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, player.media.Length);

                foreach (var media in player.media)
                {
                    string hash = CrypTo.md5($"{init.clientId}:{content_type}:{content_id}:{media.playlist}:{requestInfo.IP}");
                    string link = accsArgs($"{host}/lite/videocdn/video?rjson={rjson}&content_id={content_id}&content_type={content_type}&playlist={HttpUtility.UrlEncode(media.playlist)}&max_quality={media.max_quality}&translation_id={media.translation_id}&hash={hash}");
                    string streamlink = link.Replace("/videocdn/video", "/videocdn/video.m3u8") + "&play=true";

                    mtpl.Append(media.translation_name, link, "call", streamlink, quality: media.max_quality?.ToString());
                }

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
            else
            {
                #region Сериал
                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);

                if (s == -1)
                {
                    var tpl = new SeasonTpl(player.media.First().max_quality?.ToString(), player.media.Length);

                    foreach (var media in player.media.OrderBy(s => s.season_id))
                    {
                        string link = $"{host}/lite/videocdn?rjson={rjson}&content_id={content_id}&content_type={content_type}&title={enc_title}&original_title={enc_original_title}&s={media.season_id}";
                        tpl.Append($"{media.season_id} сезон", link, media.season_id);
                    }

                    return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                }
                else
                {
                    #region Перевод
                    var vtpl = new VoiceTpl();
                    var tmpVoice = new HashSet<int>();

                    foreach (var media in player.media)
                    {
                        if (media.season_id != s)
                            continue;

                        foreach (var episode in media.episodes)
                        {
                            foreach (var voice in episode.media)
                            {
                                if (tmpVoice.Contains(voice.translation_id))
                                    continue;

                                tmpVoice.Add(voice.translation_id);

                                if (string.IsNullOrEmpty(t))
                                    t = voice.translation_id.ToString();

                                vtpl.Append(voice.translation_name, t == voice.translation_id.ToString(), $"{host}/lite/videocdn?rjson={rjson}&content_id={content_id}&content_type={content_type}&title={enc_title}&original_title={enc_original_title}&s={s}&t={voice.translation_id}");
                            }
                        }
                    }
                    #endregion

                    if (string.IsNullOrEmpty(t))
                        t = "0";

                    var etpl = new EpisodeTpl();
                    string sArhc = s.ToString();

                    foreach (var media in player.media)
                    {
                        if (media.season_id != s)
                            continue;

                        foreach (var episode in media.episodes)
                        {
                            foreach (var voice in episode.media)
                            {
                                if (voice.translation_id.ToString() != t)
                                    continue;

                                string hash = CrypTo.md5($"{init.clientId}:{content_type}:{content_id}:{voice.playlist}:{requestInfo.IP}");
                                string link = accsArgs($"{host}/lite/videocdn/video?content_id={content_id}&content_type={content_type}&playlist={HttpUtility.UrlEncode(voice.playlist)}&max_quality={voice.max_quality}&s={s}&e={episode.episode_id}&translation_id={voice.translation_id}&hash={hash}&serial=true");
                                string streamlink = link.Replace("/videocdn/video", "/videocdn/video.m3u8") + "&play=true";

                                etpl.Append($"{episode.episode_id} серия", title ?? original_title, sArhc, episode.episode_id.ToString(), link, "call", streamlink: streamlink);
                            }
                        }
                    }

                    if (rjson)
                        return ContentTo(etpl.ToJson(vtpl));

                    return ContentTo(vtpl.ToHtml() + etpl.ToHtml());
                }
                #endregion
            }
        }


        #region Video
        static FileStream logFileStream = null;

        [HttpGet]
        [Route("lite/videocdn/video")]
        [Route("lite/videocdn/video.m3u8")]
        async public ValueTask<ActionResult> Video(string hash, long content_id, string content_type, string playlist, int max_quality, bool play, bool serial, int s, int e, int translation_id)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (hash != CrypTo.md5($"{init.clientId}:{content_type}:{content_id}:{playlist}:{requestInfo.IP}"))
                return OnError("hash", gbcache: false);

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: serial ? -1 : null);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            string accessToken = await getToken(proxy);
            if (string.IsNullOrEmpty(accessToken))
                return OnError("token", gbcache: false);

            try
            {
                if (init.log)
                {
                    string data = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        time = DateTime.Now,
                        requestInfo.Country,
                        requestInfo.IP,
                        requestInfo.UserAgent,
                        video = new { content_id, content_type, playlist, accessToken }
                    });

                    string patchlog = $"cache/logs/VideoCDN/{DateTime.Today:dd-MM}.txt";

                    if (logFileStream == null || !System.IO.File.Exists(patchlog))
                        logFileStream = new FileStream(patchlog, FileMode.Append, FileAccess.Write);

                    var buffer = Encoding.UTF8.GetBytes($"{data}\n");
                    logFileStream.Write(buffer, 0, buffer.Length);
                    logFileStream.Flush();
                }
            }
            catch { }

            string clientIP = init.verifyip ? requestInfo.IP : "::1";
            string memkey = $"videocdn/video:{playlist}:{(init.streamproxy ? "" : clientIP)}";

            return await InvkSemaphore(init, memkey, async () =>
            {
                if (!hybridCache.TryGetValue(memkey, out string hls))
                {
                    var headers = HeadersModel.Init("Authorization", $"Bearer {accessToken}");

                    if (!init.streamproxy)
                        headers.Add(new("X-LAMPA-CLIENT-IP", clientIP));

                    var result = rch.enable ? await rch.Post<JObject>(init.apihost + playlist, "{}", headers: headers) :
                                              await Http.Post<JObject>(init.apihost + playlist, "{}", headers: headers, proxy: proxy);

                    if (result == null || !result.ContainsKey("url"))
                        return OnError(null, gbcache: false);

                    string url = result.Value<string>("url");
                    if (string.IsNullOrEmpty(url))
                        return OnError(null, gbcache: false);

                    if (url.StartsWith("/"))
                        hls = $"{init.scheme}:{url}";
                    else
                        hls = url;

                    hybridCache.Set(memkey, hls, DateTime.Now.AddMinutes(10));
                }

                if (play)
                    return Redirect(HostStreamProxy(init, hls));

                var player = await getPlayer(content_id, content_type, accessToken, proxy);
                VastConf vast = requestInfo.user != null ? null : new VastConf() { url = player?.tag_url, msg = init?.vast?.msg };
                if (init.disable_ads)
                    vast = null;

                #region subtitle
                var subtitles = new SubtitleTpl();

                try
                {
                    if (translation_id > 0)
                    {
                        if (serial)
                        {
                            if (e > 0 && s > 0)
                            {
                                foreach (var media in player.media.Where(i => i.season_id == s))
                                {
                                    foreach (var episode in media.episodes.Where(i => i.episode_id == e))
                                    {
                                        foreach (var voice in episode.media.Where(i => i.translation_id == translation_id))
                                        {
                                            if (voice.tracks != null)
                                            {
                                                foreach (var t in voice.tracks)
                                                    subtitles.Append(t.label ?? t.srlang, $"{init.scheme}:{t.src}");

                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            var tracks = player.media.FirstOrDefault(i => i.translation_id == translation_id).tracks;
                            if (tracks != null)
                            {
                                foreach (var t in tracks)
                                    subtitles.Append(t.label ?? t.srlang, $"{init.scheme}:{t.src}");
                            }
                        }
                    }
                }
                catch { }
                #endregion

                if (max_quality > 0 && !init.hls)
                {
                    var streamquality = new StreamQualityTpl();
                    foreach (int q in new int[] { 1080, 720, 480, 360, 240 })
                    {
                        if (max_quality >= q)
                            streamquality.Append(HostStreamProxy(init, Regex.Replace(hls, "/hls\\.m3u8$", $"/{q}.mp4")), $"{q}p");
                    }

                    if (!streamquality.Any())
                        return OnError("streams");

                    var first = streamquality.Firts();
                    return ContentTo(VideoTpl.ToJson("play", first.link, first.quality, streamquality: streamquality, subtitles: subtitles, vast: vast));
                }

                return ContentTo(VideoTpl.ToJson("play", HostStreamProxy(init, hls), "auto", subtitles: subtitles, vast: vast));
            });
        }
        #endregion


        #region getToken
        async ValueTask<string> getToken(WebProxy proxy)
        {
            var init = await Initialization();

            #region refreshToken
            string memKey = $"videocdn:refreshToken:{init.username}";
            if (!hybridCache.TryGetValue(memKey, out string refreshToken))
            {
                var data = new System.Net.Http.StringContent($"{{\"username\":\"{init.username}\",\"password\":\"{init.password}\"}}", Encoding.UTF8, "application/json");
                var job = await Http.Post<JObject>($"{init.apihost}/login", data, useDefaultHeaders: false, proxy: proxy);
                if (job == null || !job.ContainsKey("refreshToken"))
                    return null;

                refreshToken = job.Value<string>("refreshToken");
                if (string.IsNullOrEmpty(refreshToken))
                    return null;

                hybridCache.Set(memKey, refreshToken, DateTime.Now.AddDays(2));
            }
            #endregion

            string clientIP = init.verifyip ? requestInfo.IP : "::1";

            memKey = $"videocdn:accessToken:{(init.streamproxy ? "" : clientIP)}";
            if (!hybridCache.TryGetValue(memKey, out string accessToken))
            {
                var headers = init.streamproxy ? null : HeadersModel.Init(("X-LAMPA-CLIENT-IP", clientIP));

                var data = new System.Net.Http.StringContent($"{{\"token\":\"{refreshToken}\"}}", Encoding.UTF8, "application/json");
                var job = await Http.Post<JObject>($"{init.apihost}/refresh", data, timeoutSeconds: 5, useDefaultHeaders: false, headers: headers, proxy: proxy);
                if (job == null || !job.ContainsKey("accessToken"))
                    return null;

                accessToken = job.Value<string>("accessToken");
                if (string.IsNullOrEmpty(accessToken))
                    return null;

                hybridCache.Set(memKey, accessToken, DateTime.Now.AddMinutes(5));
            }

            return accessToken;
        }
        #endregion

        #region getPlayer
        async ValueTask<EmbedModel> getPlayer(long content_id, string content_type, string accessToken, WebProxy proxy)
        {
            if (content_id == 0 || string.IsNullOrEmpty(content_type))
                return null;

            var init = await Initialization();
            string clientIP = init.verifyip ? requestInfo.IP : "::1";

            return await InvokeCache($"videocdn:{content_id}:{content_type}:{accessToken}:{(init.streamproxy ? "" : clientIP)}", TimeSpan.FromMinutes(5), async () =>
            {
                var headers = HeadersModel.Init(
                    ("Authorization", $"Bearer {accessToken}"),
                    ("User-Agent", HttpContext.Request.Headers.UserAgent)
                );

                if (!init.streamproxy)
                    headers.Add(new("X-LAMPA-CLIENT-IP", clientIP));

                string json = await Http.Get($"{init.apihost}/stream?clientId={init.clientId}&contentType={content_type}&contentId={content_id}&domain={init.domain}", useDefaultHeaders: false, timeoutSeconds: 8, headers: headers, proxy: proxy);
                if (string.IsNullOrEmpty(json))
                    return null;

                var job = JsonConvert.DeserializeObject<JObject>(json);

                var model = job["player"].ToObject<EmbedModel>();
                model.tag_url = job["ads"]["rolls"].First.Value<string>("tag_url");

                return model;
            });
        }
        #endregion

        #region Search
        async ValueTask<(long content_id, string content_type, SimilarTpl similar)> Search(LumexSettings init, string imdb_id, long kinopoisk_id, string title, string original_title, int serial, int clarification, bool similar, WebProxy proxy)
        {
            async Task<JToken> searchId(string imdb_id, long kinopoisk_id)
            {
                if (string.IsNullOrEmpty(init.token))
                    return null;

                if (string.IsNullOrEmpty(imdb_id) && kinopoisk_id == 0)
                    return null;

                string arg = kinopoisk_id > 0 ? $"&kinopoisk_id={kinopoisk_id}" : $"&imdb_id={imdb_id}";
                var job = await Http.Get<JObject>($"{init.iframehost}/api/short?api_token={init.token}" + arg, timeoutSeconds: 8, proxy: proxy);
                if (job == null || !job.ContainsKey("data"))
                    return null;

                var result = job["data"]?.First;
                if (result == null)
                    return null;

                return result;
            }

            var movie = similar ? null : (await searchId(imdb_id, 0) ?? await searchId(null, kinopoisk_id));
            if (movie != null)
            {
                return (movie.Value<long>("id"), movie.Value<string>("content_type"), default);
            }
            else
            {
                if (string.IsNullOrEmpty(title ?? original_title) || string.IsNullOrEmpty(init.token))
                    return default;

                string uri = $"{init.iframehost}/api/short?api_token={init.token}&title={HttpUtility.UrlEncode(clarification == 1 ? title : (original_title ?? title))}";
                string json = await Http.Get(uri, timeoutSeconds: 8, proxy: proxy);
                if (json == null)
                    return default;

                SearchRoot root;

                try
                {
                    root = JsonConvert.DeserializeObject<SearchRoot>(json);
                    if (root?.data == null || root.data.Length == 0)
                        return default;
                }
                catch { return default; }

                var stpl = new SimilarTpl(root.data.Length);

                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);

                int count = 0;
                foreach (var item in root.data)
                {
                    if (serial != -1)
                    {
                        if ((serial == 0 && item.content_type != "movie") || (serial == 1 && item.content_type == "movie"))
                            continue;
                    }

                    if (clarification != 1)
                    {
                        bool isok = title != null && title.Length > 3 && item.title != null && item.title.ToLower().Contains(title.ToLower());
                        isok = isok ? true : original_title != null && original_title.Length > 3 && item.orig_title != null && item.orig_title.ToLower().Contains(original_title.ToLower());

                        if (!isok)
                            continue;
                    }

                    string year = item.add?.Split("-")?[0] ?? string.Empty;
                    string name = !string.IsNullOrEmpty(item.title) && !string.IsNullOrEmpty(item.orig_title) ? $"{item.title} / {item.orig_title}" : (item.title ?? item.orig_title);

                    string details = $"imdb: {item.imdb_id} {stpl.OnlineSplit} kinopoisk: {item.kp_id}";

                    string img = PosterApi.Find(item.kp_id, item.imdb_id);
                    stpl.Append(name, year, details, $"{host}/lite/videocdn?title={enc_title}&original_title={enc_original_title}&content_id={item.id}&content_type={item.content_type}", img);

                    count += 1;
                }

                return (0, null, stpl);
            }
        }
        #endregion
    }
}

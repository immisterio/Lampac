using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Alloha;
using Shared.Models.Online.Settings;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class Alloha : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager(AppInit.conf.Alloha);

        #region Initialization
        ValueTask<AllohaSettings> Initialization()
        {
            return loadKit(AppInit.conf.Alloha, (j, i, c) =>
            {
                if (j.ContainsKey("m4s"))
                    i.m4s = c.m4s;

                if (j.ContainsKey("linkhost"))
                    i.linkhost = c.linkhost;

                if (j.ContainsKey("reserve"))
                    i.reserve = c.reserve;

                i.secret_token = c.secret_token;
                i.token = c.token;
                return i;
            });
        }
        #endregion

        [HttpGet]
        [Route("lite/alloha")]
        async public ValueTask<ActionResult> Index(string orid, string imdb_id, long kinopoisk_id, string title, string original_title, int serial, string original_language, int year, string t, int s = -1, bool origsource = false, bool rjson = false, bool similar = false)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (similar)
                return await SpiderSearch(title, origsource, rjson);

            var result = await search(init, orid, imdb_id, kinopoisk_id, title, serial, original_language, year);
            if (result.category_id == 0)
                return OnError("data", proxyManager, result.refresh_proxy);

            if (result.data == null)
                return Ok();

            if (origsource)
                return ContentTo(JsonConvert.SerializeObject(result.data));

            JToken data = result.data;

            string defaultargs = $"&orid={orid}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&serial={serial}&year={year}&original_language={original_language}";

            if (result.category_id is 1 or 3)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);
                bool directors_cut = data.Value<bool>("available_directors_cut");

                foreach (var translation in data.Value<JObject>("translation_iframe").ToObject<Dictionary<string, Dictionary<string, object>>>())
                {
                    string link = $"{host}/lite/alloha/video?t={translation.Key}&token_movie={result.data.Value<string>("token_movie")}" + defaultargs;
                    string streamlink = accsArgs($"{link.Replace("/video", "/video.m3u8")}&play=true");

                    bool uhd = false;
                    if (translation.Value.TryGetValue("uhd", out object _uhd))
                        uhd = _uhd.ToString().ToLower() == "true" && init.m4s;

                    if (directors_cut && translation.Key == "66")
                        mtpl.Append("Режиссерская версия", $"{link}&directors_cut=true", "call", $"{streamlink}&directors_cut=true", voice_name: uhd ? "2160p" : translation.Value["quality"].ToString(), quality: uhd ? "2160p" : "");

                    mtpl.Append(translation.Value["name"].ToString(), link, "call", streamlink, voice_name: uhd ? "2160p" : translation.Value["quality"].ToString(), quality: uhd ? "2160p" : "");
                }

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
            else
            {
                #region Сериал
                if (s == -1)
                {
                    var tpl = new SeasonTpl(result.data.Value<bool>("uhd") && init.m4s ? "2160p" : null);

                    foreach (var season in data.Value<JObject>("seasons").ToObject<Dictionary<string, object>>().Reverse())
                        tpl.Append($"{season.Key} сезон", $"{host}/lite/alloha?rjson={rjson}&s={season.Key}{defaultargs}", season.Key);

                    return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                }
                else
                {
                    #region Перевод
                    var vtpl = new VoiceTpl();
                    var temp_translation = new HashSet<string>();

                    string activTranslate = t;

                    foreach (var episodes in data.Value<JObject>("seasons").GetValue(s.ToString()).Value<JObject>("episodes").ToObject<Dictionary<string, Episode>>().Select(i => i.Value.translation))
                    {
                        foreach (var translation in episodes)
                        {
                            if (temp_translation.Contains(translation.Value.translation) || translation.Value.translation.ToLower().Contains("субтитры"))
                                continue;

                            temp_translation.Add(translation.Value.translation);

                            if (string.IsNullOrWhiteSpace(activTranslate))
                                activTranslate = translation.Key;

                            vtpl.Append(translation.Value.translation, activTranslate == translation.Key, $"{host}/lite/alloha?rjson={rjson}&s={s}&t={translation.Key}{defaultargs}");
                        }
                    }
                    #endregion

                    var etpl = new EpisodeTpl();
                    string sArhc = s.ToString();

                    foreach (var episode in data.Value<JObject>("seasons").GetValue(sArhc).Value<JObject>("episodes").ToObject<Dictionary<string, Episode>>().Reverse())
                    {
                        if (!string.IsNullOrWhiteSpace(activTranslate) && !episode.Value.translation.ContainsKey(activTranslate))
                            continue;

                        string link = $"{host}/lite/alloha/video?t={activTranslate}&s={s}&e={episode.Key}&token_movie={result.data.Value<string>("token_movie")}" + defaultargs;
                        string streamlink = accsArgs($"{link.Replace("/video", "/video.m3u8")}&play=true");

                        etpl.Append($"{episode.Key} серия", title ?? original_title, sArhc, episode.Key, link, "call", streamlink: streamlink);
                    }

                    if (rjson)
                        return ContentTo(etpl.ToJson(vtpl));

                    return ContentTo(vtpl.ToHtml() + etpl.ToHtml());
                }
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/alloha/video")]
        [Route("lite/alloha/video.m3u8")]
        async public ValueTask<ActionResult> Video(string token_movie, string title, string original_title, string t, int s, int e, bool play, bool directors_cut)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var proxy = proxyManager.BaseGet();

            return await InvkSemaphore(init, $"alloha:view:stream:{init.secret_token}:{token_movie}:{t}:{s}:{e}:{init.m4s}:{directors_cut}", async () =>
            {
                if (!string.IsNullOrEmpty(init.secret_token))
                {
                    #region Прямые ссылки
                    string userIp = requestInfo.IP;
                    if (init.localip || init.streamproxy)
                    {
                        userIp = await mylocalip();
                        if (userIp == null)
                            return OnError("userIp");
                    }

                    string memKey = $"alloha:view:stream:{init.secret_token}:{token_movie}:{t}:{s}:{e}:{userIp}:{init.m4s}:{directors_cut}";
                    if (!hybridCache.TryGetValue(memKey, out JToken data))
                    {
                        #region url запроса
                        string uri = $"{init.linkhost}/direct?secret_token={init.secret_token}&token_movie={token_movie}";

                        uri += $"&ip={userIp}&translation={t}";

                        if (s > 0)
                            uri += $"&season={s}";

                        if (e > 0)
                            uri += $"&episode={e}";

                        if (init.m4s)
                            uri += "&av1=true";

                        if (directors_cut)
                            uri += "&directors_cut";
                        #endregion

                        var root = await Http.Get<JObject>(uri, timeoutSeconds: 8, proxy: proxy.proxy, headers: httpHeaders(init));
                        if (root == null)
                            return OnError("json", proxyManager);

                        if (!root.ContainsKey("data"))
                            return OnError("data");

                        proxyManager.Success();

                        data = root["data"];
                        hybridCache.Set(memKey, data, cacheTime(10, init: init));
                    }

                    #region subtitle
                    var subtitles = new SubtitleTpl();

                    try
                    {
                        foreach (var sub in data["file"]["tracks"])
                            subtitles.Append(sub.Value<string>("label"), sub.Value<string>("src"));
                    }
                    catch { }
                    #endregion

                    List<(string link, string quality)> streams = null;

                    foreach (var hlsSource in data["file"]["hlsSource"])
                    {
                        // first or default
                        if (streams == null || hlsSource.Value<bool>("default"))
                        {
                            streams = new List<(string link, string quality)>(6);

                            foreach (var q in hlsSource["quality"].ToObject<Dictionary<string, string>>())
                            {
                                string file = q.Value;
                                if (init.reserve)
                                    file += " or " + hlsSource["reserve"][q.Key].ToString();

                                streams.Add((HostStreamProxy(init, file, proxy: proxy.proxy), $"{q.Key}p"));
                            }
                        }
                    }

                    if (streams == null || streams.Count == 0)
                        return OnError("streams");

                    var streamquality = new StreamQualityTpl(streams);

                    if (play)
                        return RedirectToPlay(streamquality.Firts().link);

                    #region segments
                    var segments = new SegmentTpl();

                    var dfile = data["file"];
                    string skipTime = dfile.Value<string>("skipTime");
                    string removeTime = dfile.Value<string>("removeTime");

                    if (skipTime != null && skipTime.Contains("-"))
                    {
                        var t = skipTime.Split('-');
                        if (t.Length == 2 && int.TryParse(t[0], out int start) && int.TryParse(t[1], out int end))
                            segments.skip(start, end);
                    }

                    if (removeTime != null && removeTime.Contains("-"))
                    {
                        var t = removeTime.Split('-');
                        if (t.Length == 2 && int.TryParse(t[0], out int start) && int.TryParse(t[1], out int end))
                            segments.ad(start, end);
                    }
                    #endregion

                    return ContentTo(VideoTpl.ToJson("play", streamquality.Firts().link, (title ?? original_title),
                        streamquality: streamquality,
                        vast: init.vast,
                        subtitles: subtitles,
                        segments: segments,
                        hls_manifest_timeout: (int)TimeSpan.FromSeconds(20).TotalMilliseconds
                    ));
                    #endregion
                }
                else
                {
                    #region Playwright
                    init.streamproxy = true; // force streamproxy

                    string memKey = $"alloha:black_magic:{proxy.data.ip}:{token_movie}:{t}:{s}:{e}";
                    if (!hybridCache.TryGetValue(memKey, out (JToken hlsSource, JToken tracks, List<HeadersModel> streamHeaders) cache))
                    {
                        if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                            return OnError();

                        using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                        {
                            var headers = HeadersModel.Init(Http.defaultFullHeaders,
                                ("sec-fetch-dest", "iframe"),
                                ("sec-fetch-mode", "navigate"),
                                ("sec-fetch-site", "cross-site"),
                                ("referer", $"https://alloha.tv/")
                            );

                            var page = await browser.NewPageAsync(init.plugin, proxy: proxy.data, headers: headers.ToDictionary()).ConfigureAwait(false);
                            if (page == null)
                                return null;

                            await page.RouteAsync("**/*", async route =>
                            {
                                try
                                {
                                    if (browser.IsCompleted || route.Request.Url.Contains("/master.m3u8") || route.Request.Url.Contains("blank.mp4") || route.Request.Url.Contains("googleapis.com"))
                                    {
                                        PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                        await route.AbortAsync();
                                        return;
                                    }

                                    if (route.Request.Url.Contains("/apis/movies/"))
                                    {
                                        await route.ContinueAsync();
                                        var response = await page.WaitForResponseAsync(route.Request.Url);
                                        string result = response != null ? await response.TextAsync() : string.Empty;
                                        browser.SetPageResult(result);
                                        PlaywrightBase.WebLog(route.Request, response, result);
                                        return;
                                    }

                                    if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                        return;

                                    await route.ContinueAsync();
                                }
                                catch { }
                            });

                            string targetUrl = $"{init.linkhost}/?token_movie={token_movie}&translation={t}&token={init.token}";
                            if (s > 0)
                                targetUrl += $"&season={s}&episode={e}";

                            PlaywrightBase.GotoAsync(page, PlaywrightBase.IframeUrl(targetUrl/* + "&autoplay=1"*/));

                            try
                            {
                                var root = JsonConvert.DeserializeObject<JObject>(await browser.WaitPageResult(20));

                                foreach (var item in root["hlsSource"])
                                {
                                    if (item?.Value<bool>("default") == true)
                                    {
                                        cache.hlsSource = item;
                                        break;
                                    }
                                }

                                if (cache.hlsSource == null)
                                    cache.hlsSource = root["hlsSource"].First;

                                if (root.ContainsKey("tracks"))
                                    cache.tracks = root["tracks"];
                            }
                            catch { }
                        }

                        if (cache.hlsSource == null)
                            return OnError();

                        cache.streamHeaders = HeadersModel.Init(Http.defaultFullHeaders,
                            ("origin", init.linkhost),
                            ("referer", $"{init.linkhost}/?token_movie={token_movie}&translation={t}&token={init.token}"),
                            ("sec-fetch-dest", "empty"),
                            ("sec-fetch-mode", "cors"),
                            ("sec-fetch-site", "cross-site")
                        );

                        hybridCache.Set(memKey, cache, cacheTime(20, init: init));
                    }

                    var streamquality = new StreamQualityTpl();

                    foreach (var q in cache.hlsSource["quality"].ToObject<Dictionary<string, string>>())
                    {
                        if (!init.m4s && (q.Key is "2160" or "1440"))
                            continue;

                        string link = init.reserve ? q.Value : Regex.Match(q.Value, "(https?://[^\n\r\t ]+/[^\\.]+\\.m3u8)").Groups[1].Value;
                        streamquality.Append(HostStreamProxy(init, link, headers: cache.streamHeaders), $"{q.Key}p");
                    }

                    if (play)
                        return RedirectToPlay(streamquality.Firts().link);

                    #region subtitle
                    var subtitles = new SubtitleTpl();

                    try
                    {
                        if (cache.tracks != null)
                        {
                            foreach (var sub in cache.tracks)
                                subtitles.Append(sub.Value<string>("label"), HostStreamProxy(init, sub.Value<string>("src"), headers: cache.streamHeaders));
                        }
                    }
                    catch { }
                    #endregion

                    return ContentTo(VideoTpl.ToJson("play", streamquality.Firts().link, cache.hlsSource.Value<string>("label"),
                           streamquality: streamquality,
                           subtitles: subtitles,
                           vast: init.vast,
                           headers: cache.streamHeaders
                    ));
                    #endregion
                }
            });
        }
        #endregion

        #region SpiderSearch
        [HttpGet]
        [Route("lite/alloha-search")]
        async public ValueTask<ActionResult> SpiderSearch(string title, bool origsource = false, bool rjson = false)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var cache = await InvokeCache<JArray>($"alloha:search:{title}", cacheTime(40, init: init), proxyManager, async res =>
            {
                var root = await Http.Get<JObject>($"{init.apihost}/?token={init.token}&name={HttpUtility.UrlEncode(title)}&list", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                if (root == null || !root.ContainsKey("data"))
                    return res.Fail("data");

                return root["data"].ToObject<JArray>();
            });

            return OnResult(cache, () =>
            {
                var stpl = new SimilarTpl(cache.Value.Count);

                foreach (var j in cache.Value)
                {
                    string uri = $"{host}/lite/alloha?orid={j.Value<string>("token_movie")}";
                    stpl.Append(j.Value<string>("name") ?? j.Value<string>("original_name"), j.Value<int>("year").ToString(), string.Empty, uri, PosterApi.Size(j.Value<string>("poster")));
                }

                return rjson ? stpl.ToJson() : stpl.ToHtml();

            }, origsource: origsource);
        }
        #endregion


        #region search
        async ValueTask<(bool refresh_proxy, int category_id, JToken data)> search(AllohaSettings init, string token_movie, string imdb_id, long kinopoisk_id, string title, int serial, string original_language, int year)
        {
            string memKey = $"alloha:view:{kinopoisk_id}:{imdb_id}";
            if (0 >= kinopoisk_id && string.IsNullOrEmpty(imdb_id))
                memKey = $"alloha:viewsearch:{title}:{serial}:{original_language}:{year}";

            if (!string.IsNullOrEmpty(token_movie))
                memKey = $"alloha:view:{token_movie}";

            JObject root = null;

            if (!hybridCache.TryGetValue(memKey, out (int category_id, JToken data) res))
            {
                if (memKey.Contains(":viewsearch:"))
                {
                    if (string.IsNullOrWhiteSpace(title) || year == 0)
                        return default;

                    root = await Http.Get<JObject>($"{init.apihost}/?token={init.token}&name={HttpUtility.UrlEncode(title)}&list={(serial == 1 ? "serial" : "movie")}", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (root == null)
                        return (true, 0, null);

                    if (root.ContainsKey("data"))
                    {
                        string stitle = title.ToLower();

                        foreach (var item in root["data"])
                        {
                            if (item.Value<string>("name")?.ToLower()?.Trim() == stitle)
                            {
                                int y = item.Value<int>("year");
                                if (y > 0 && (y == year || y == (year - 1) || y == (year + 1)))
                                {
                                    if (original_language == "ru" && item.Value<string>("country")?.ToLower() != "россия")
                                        continue;

                                    res.data = item;
                                    res.category_id = item.Value<int>("category_id");
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(imdb_id))
                        root = await Http.Get<JObject>($"{init.apihost}/?token={init.token}&imdb={imdb_id}&token_movie={token_movie}", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    
                    if ((root == null || !root.ContainsKey("data")) && kinopoisk_id > 0)
                        root = await Http.Get<JObject>($"{init.apihost}/?token={init.token}&kp={kinopoisk_id}&token_movie={token_movie}", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));

                    if (root == null)
                        return (true, 0, null);

                    if (root.ContainsKey("data"))
                    {
                        res.data = root.GetValue("data");
                        res.category_id = res.data.Value<int>("category");
                    }
                }

                if (res.data != null)
                    proxyManager.Success();

                if (res.data != null || (root.ContainsKey("error_info") && root.Value<string>("error_info") == "not movie"))
                    hybridCache.Set(memKey, res, cacheTime(res.category_id is 1 or 3 ? 120 : 40, init: init));
                else
                    hybridCache.Set(memKey, res, cacheTime(2, init: init));
            }

            return (false, res.category_id, res.data);
        }
        #endregion
    }
}

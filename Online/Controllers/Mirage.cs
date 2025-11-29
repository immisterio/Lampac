using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Settings;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class Mirage : BaseOnlineController
    {
        ValueTask<AllohaSettings> Initialization()
        {
            return loadKit(AppInit.conf.Mirage, (j, i, c) =>
            {
                if (j.ContainsKey("m4s"))
                    i.m4s = c.m4s;
                return i;
            });
        }

        [HttpGet]
        [Route("lite/mirage")]
        async public ValueTask<ActionResult> Index(string orid, string imdb_id, long kinopoisk_id, string title, string original_title, int serial, string original_language, int year, int t = -1, int s = -1, bool origsource = false, bool rjson = false, bool similar = false)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: serial == 0 ? null : -1);
            if (rch.IsRequiredConnected())
                return ContentTo(rch.connectionMsg);

            if (similar)
                return await SpiderSearch(title, origsource, rjson);

            var result = await search(orid, imdb_id, kinopoisk_id, title, serial, original_language, year);
            if (result.category_id == 0 || result.data == null)
                return OnError();

            JToken data = result.data;
            string tokenMovie = data["token_movie"] != null ? data.Value<string>("token_movie") : null;
            var frame = await iframe(tokenMovie, init);
            if (frame.all == null)
                return OnError();

            //return ContentTo(JsonConvert.SerializeObject(frame.all));

            if (result.category_id is 1 or 3)
            {
                #region Фильм
                var videos = frame.all["theatrical"].ToObject<Dictionary<string, Dictionary<string, JObject>>>();

                var mtpl = new MovieTpl(title, original_title, videos.Count);

                foreach (var i in videos)
                {
                    var file = i.Value.First().Value;

                    string translation = file.Value<string>("translation");
                    string quality = file.Value<string>("quality");
                    long id = file.Value<long>("id");
                    bool uhd = init.m4s ? file.Value<bool>("uhd") : false;

                    string link = $"{host}/lite/mirage/video?id_file={id}&token_movie={data.Value<string>("token_movie")}";
                    string streamlink = accsArgs($"{link.Replace("/video", "/video.m3u8")}&play=true");

                    mtpl.Append(translation, link, "call", streamlink, voice_name: uhd ? "2160p" : quality, quality: uhd ? "2160p" : "");
                }

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
            else
            {
                #region Сериал
                string defaultargs = $"&orid={orid}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&year={year}&original_language={original_language}";

                if (s == -1)
                {
                    #region Сезоны
                    string q = null;
                    try
                    {
                        if (init.m4s)
                            q = frame.active.Value<bool>("uhd") == true ? "2160p" : null;
                    }
                    catch { }

                    Dictionary<string, JToken> seasons;
                    if (frame.all["seasons"] != null)
                        seasons = frame.all["seasons"].ToObject<Dictionary<string, JToken>>();
                    else
                        seasons = frame.all.ToObject<Dictionary<string, JToken>>();

                    if (seasons.First().Key.StartsWith("t"))
                    {
                        var tpl = new SeasonTpl(q);

                        var seasonNumbers = new HashSet<int>();

                        foreach (var translation in seasons)
                        {
                            var file = translation.Value["file"];
                            if (file == null)
                                continue;

                            foreach (var season in file.ToObject<Dictionary<string, object>>())
                            {
                                if (int.TryParse(season.Key, out int seasonNumber))
                                    seasonNumbers.Add(seasonNumber);
                            }
                        }

                        if (!seasonNumbers.Any())
                            seasonNumbers.Add(frame.active.Value<int>("seasons"));

                        foreach (int i in seasonNumbers.OrderBy(i => i))
                            tpl.Append($"{i} сезон", $"{host}/lite/mirage?rjson={rjson}&s={i}{defaultargs}", i.ToString());

                        return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                    }
                    else
                    {
                        var tpl = new SeasonTpl(q, seasons.Count);

                        foreach (var season in seasons)
                            tpl.Append($"{season.Key} сезон", $"{host}/lite/mirage?rjson={rjson}&s={season.Key}{defaultargs}", season.Key);

                        return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                    }
                    #endregion
                }
                else
                {
                    var vtpl = new VoiceTpl();
                    var etpl = new EpisodeTpl();
                    var voices = new HashSet<int>();

                    string sArhc = s.ToString();

                    if (frame.all[sArhc] is JArray)
                    {
                        #region Перевод
                        foreach (var episode in frame.all[sArhc])
                        {
                            foreach (var voice in episode.ToObject<Dictionary<string, JObject>>().Select(i => i.Value))
                            {
                                int id_translation = voice.Value<int>("id_translation");
                                if (voices.Contains(id_translation))
                                    continue;

                                voices.Add(id_translation);

                                if (t == -1)
                                    t = id_translation;

                                string link = $"{host}/lite/mirage?rjson={rjson}&s={s}&t={id_translation}{defaultargs}";
                                bool active = t == id_translation;

                                vtpl.Append(voice.Value<string>("translation"), active, link);
                            }
                        }
                        #endregion

                        foreach (var episode in frame.all[sArhc])
                        {
                            foreach (var voice in episode.ToObject<Dictionary<string, JObject>>().Select(i => i.Value))
                            {
                                if (voice.Value<int>("id_translation") != t)
                                    continue;

                                string translation = voice.Value<string>("translation");
                                int e = voice.Value<int>("episode");

                                string link = $"{host}/lite/mirage/video?id_file={voice.Value<long>("id")}&token_movie={data.Value<string>("token_movie")}";
                                string streamlink = accsArgs($"{link.Replace("/video", "/video.m3u8")}&play=true");

                                if (e > 0)
                                    etpl.Append($"{e} серия", title ?? original_title, sArhc, e.ToString(), link, "call", voice_name: translation, streamlink: streamlink);
                            }
                        }
                    }
                    else if (frame.all.ToObject<Dictionary<string, object>>().First().Key.StartsWith("t"))
                    {
                        #region Перевод
                        foreach (var node in frame.all)
                        {
                            if (!node.First["file"].ToObject<Dictionary<string, object>>().ContainsKey(sArhc))
                                continue;

                            var voice = node.First["file"].First.First.First.First;
                            int id_translation = voice.Value<int>("id_translation");
                            if (voices.Contains(id_translation))
                                continue;

                            voices.Add(id_translation);

                            if (t == -1)
                                t = id_translation;

                            string link = $"{host}/lite/mirage?rjson={rjson}&s={s}&t={id_translation}{defaultargs}";
                            bool active = t == id_translation;

                            vtpl.Append(voice.Value<string>("translation"), active, link);
                        }
                        #endregion

                        foreach (var node in frame.all)
                        {
                            foreach (var season in node.First["file"].ToObject<Dictionary<string, object>>())
                            {
                                if (season.Key != sArhc)
                                    continue;

                                if (season.Value is JArray sjar)
                                {

                                }
                                else if (season.Value is JObject sjob)
                                {
                                    foreach (var episode in sjob.ToObject<Dictionary<string, JObject>>())
                                    {
                                        if (episode.Value.Value<int>("id_translation") != t)
                                            continue;

                                        string translation = episode.Value.Value<string>("translation");
                                        int e = episode.Value.Value<int>("episode");

                                        string link = $"{host}/lite/mirage/video?id_file={episode.Value.Value<long>("id")}&token_movie={data.Value<string>("token_movie")}";
                                        string streamlink = accsArgs($"{link.Replace("/video", "/video.m3u8")}&play=true");

                                        if (e > 0)
                                            etpl.Append($"{e} серия", title ?? original_title, sArhc, e.ToString(), link, "call", voice_name: translation, streamlink: streamlink);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        #region Перевод
                        foreach (var episode in frame.all[sArhc].ToObject<Dictionary<string, Dictionary<string, JObject>>>())
                        {
                            foreach (var voice in episode.Value.Select(i => i.Value))
                            {
                                int id_translation = voice.Value<int>("id_translation");
                                if (voices.Contains(id_translation))
                                    continue;

                                voices.Add(id_translation);

                                if (t == -1)
                                    t = id_translation;

                                string link = $"{host}/lite/mirage?rjson={rjson}&s={s}&t={id_translation}{defaultargs}";
                                bool active = t == id_translation;

                                vtpl.Append(voice.Value<string>("translation"), active, link);
                            }
                        }
                        #endregion

                        foreach (var episode in frame.all[sArhc].ToObject<Dictionary<string, Dictionary<string, JObject>>>())
                        {
                            foreach (var voice in episode.Value.Select(i => i.Value))
                            {
                                string translation = voice.Value<string>("translation");
                                if (voice.Value<int>("id_translation") != t)
                                    continue;

                                string link = $"{host}/lite/mirage/video?id_file={voice.Value<long>("id")}&token_movie={data.Value<string>("token_movie")}";
                                string streamlink = accsArgs($"{link.Replace("/video", "/video.m3u8")}&play=true");

                                etpl.Append($"{episode.Key} серия", title ?? original_title, sArhc, episode.Key, link, "call", voice_name: translation, streamlink: streamlink);
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
        [HttpGet]
        [Route("lite/mirage/video")]
        [Route("lite/mirage/video.m3u8")]
        async public ValueTask<ActionResult> Video(long id_file, string token_movie, bool play)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (!play && rch.IsRequiredConnected())
                return ContentTo(rch.connectionMsg);

            string memKey = $"mirage:video:{id_file}:{init.m4s}";
            if (!hybridCache.TryGetValue(memKey, out (string hls, List<HeadersModel> headers) movie))
            {
                movie = await goMovie($"{init.linkhost}/?token_movie={token_movie}&token={init.token}", id_file, init);
                if (movie.hls == null)
                    return OnError();

                hybridCache.Set(memKey, movie, cacheTime(10));
            }

            var streamquality = new StreamQualityTpl();
            streamquality.Append(HostStreamProxy(init, movie.hls, headers: movie.headers), "auto");

            if (play)
                return Redirect(streamquality.Firts().link);

            return ContentTo(VideoTpl.ToJson("play", streamquality.Firts().link, "auto",
                streamquality: streamquality,
                vast: init.vast,
                headers: movie.headers,
                hls_manifest_timeout: (int)TimeSpan.FromSeconds(20).TotalMilliseconds
            ));
        }
        #endregion

        #region iframe
        async ValueTask<(JToken all, JToken active)> iframe(string token_movie, AllohaSettings init)
        {
            if (string.IsNullOrEmpty(token_movie))
                return default;

            string memKey = $"mirage:iframe:{token_movie}";
            if (!hybridCache.TryGetValue(memKey, out (JToken all, JToken active) cache))
            {
                string uri = $"{init.linkhost}/?token_movie={token_movie}&token={init.token}";
                string referer = $"https://lgfilm.fun/" + reffers[Random.Shared.Next(0, reffers.Length)];

                string html = await Http.Get(uri, httpversion: 2, timeoutSeconds: 8, headers: httpHeaders(init, HeadersModel.Init(
                    ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                    ("referer", referer),
                    ("sec-fetch-dest", "iframe"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "cross-site"),
                    ("upgrade-insecure-requests", "1")
                )));

                string json = Regex.Match(html ?? "", "fileList = JSON.parse\\('([^\n\r]+)'\\);").Groups[1].Value;
                if (string.IsNullOrEmpty(json))
                    return default;

                try
                {
                    var root = JsonConvert.DeserializeObject<JObject>(json);
                    if (root == null || !root.ContainsKey("all"))
                        return default;

                    cache = (root["all"], root["active"]);

                    hybridCache.Set(memKey, cache, cacheTime(40));
                }
                catch { return default; }
            }

            return cache;
        }
        #endregion

        #region goMovie
        async Task<(string hls, List<HeadersModel> headers)> goMovie(string uri, long id_file, AllohaSettings init)
        {
            try
            {
                using (var browser = new PlaywrightBrowser())
                {
                    var page = await browser.NewPageAsync(init.plugin).ConfigureAwait(false);
                    if (page == null)
                        return default;

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            if (route.Request.Url.Contains("lgfilm.fun"))
                            {
                                await route.FulfillAsync(new RouteFulfillOptions
                                {
                                    Body = PlaywrightBase.IframeHtml(uri)
                                });
                            }
                            else if (route.Request.Method == "POST" && route.Request.Url.Contains("/movies/"))
                            {
                                string newUrl = Regex.Replace(route.Request.Url, "/[0-9]+$", $"/{id_file}");

                                var fetchHeaders = route.Request.Headers;
                                fetchHeaders.TryAdd("accept-encoding", "gzip, deflate, br, zstd");
                                fetchHeaders.TryAdd("cache-control", "no-cache");
                                fetchHeaders.TryAdd("dnt", "1");
                                fetchHeaders.TryAdd("pragma", "no-cache");
                                fetchHeaders.TryAdd("priority", "u=1, i");
                                fetchHeaders.TryAdd("sec-fetch-dest", "empty");
                                fetchHeaders.TryAdd("sec-fetch-mode", "cors");
                                fetchHeaders.TryAdd("sec-fetch-site", "same-origin");
                                fetchHeaders.TryAdd("sec-fetch-storage-access", "active");

                                var fetchResponse = await route.FetchAsync(new RouteFetchOptions
                                {
                                    Url = newUrl,
                                    Method = "POST",
                                    Headers = fetchHeaders,
                                    PostData = route.Request.PostDataBuffer
                                }).ConfigureAwait(false);

                                string body = await fetchResponse.TextAsync().ConfigureAwait(false);

                                string targetStream = null;
                                if (init.m4s)
                                    targetStream = Regex.Match(body, "\"(2160|1440)\":\"([^\"]+)\"").Groups[2].Value;

                                if (string.IsNullOrWhiteSpace(targetStream))
                                    targetStream = Regex.Match(body, "\"(1080|720)\":\"([^\"]+)\"").Groups[2].Value;

                                if (!string.IsNullOrWhiteSpace(targetStream))
                                    body = Regex.Replace(body, "\"(2160|1440|1080|720|480|360)\":\"[^\"]+\"", $"\"$1\":\"{targetStream}\"");

                                await route.FulfillAsync(new RouteFulfillOptions
                                {
                                    Status = fetchResponse.Status,
                                    Body = body,
                                    Headers = fetchResponse.Headers
                                }).ConfigureAwait(false);
                            }
                            else
                            {
                                if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                    return;

                                await route.ContinueAsync();
                            }
                        }
                        catch { }
                    });

                    page.Response += Page_Response;

                    PlaywrightBase.GotoAsync(page, $"https://lgfilm.fun/" + reffers[Random.Shared.Next(0, reffers.Length)]);

                    try
                    {
                        return await tcsPageResponse.Task.WaitAsync(TimeSpan.FromSeconds(15));
                    }
                    catch { }
                    finally
                    {
                        page.Response -= Page_Response;
                    }
                }
            }
            catch { }

            return default;
        }

        TaskCompletionSource<(string hls, List<HeadersModel> headers)> tcsPageResponse = new TaskCompletionSource<(string hls, List<HeadersModel> headers)>();

        private void Page_Response(object sender, IResponse e)
        {
            if (e.Request.Method == "GET" && e.Url.Contains("/master.m3u8"))
            {
                var headers = HeadersModel.Init(Http.defaultFullHeaders,
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "cross-site")
                );

                foreach (var item in e.Request.Headers)
                {
                    if (item.Key.ToLower() is "host" or "accept-encoding" or "connection" or "range")
                        continue;

                    if (!Http.defaultFullHeaders.ContainsKey(item.Key.ToLower()))
                        headers.Add(new HeadersModel(item.Key, item.Value.ToString()));
                }

                tcsPageResponse.SetResult((e.Url, headers));
            }
        }
        #endregion


        #region SpiderSearch
        [HttpGet]
        [Route("lite/mirage-search")]
        async public ValueTask<ActionResult> SpiderSearch(string title, bool origsource = false, bool rjson = false)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var cache = await InvokeCache<JArray>($"mirage:search:{title}", cacheTime(40, init: init), proxyManager, async res =>
            {
                var root = await Http.Get<JObject>($"{init.apihost}/?token={init.token}&name={HttpUtility.UrlEncode(title)}&list", timeoutSeconds: 8, proxy: proxyManager.Get());
                if (root == null || !root.ContainsKey("data"))
                    return res.Fail("data");

                return root["data"].ToObject<JArray>();
            });

            return OnResult(cache, () =>
            {
                var stpl = new SimilarTpl(cache.Value.Count);

                foreach (var j in cache.Value)
                {
                    string uri = $"{host}/lite/mirage?orid={j.Value<string>("token_movie")}";
                    stpl.Append(j.Value<string>("name") ?? j.Value<string>("original_name"), j.Value<int>("year").ToString(), string.Empty, uri, PosterApi.Size(j.Value<string>("poster")));
                }

                return rjson ? stpl.ToJson() : stpl.ToHtml();

            }, origsource: origsource);
        }
        #endregion

        #region search
        async ValueTask<(bool refresh_proxy, int category_id, JToken data)> search(string token_movie, string imdb_id, long kinopoisk_id, string title, int serial, string original_language, int year)
        {
            var init = await Initialization();
            string memKey = $"mirage:view:{kinopoisk_id}:{imdb_id}";
            if (0 >= kinopoisk_id && string.IsNullOrEmpty(imdb_id))
                memKey = $"mirage:viewsearch:{title}:{serial}:{original_language}:{year}";

            if (!string.IsNullOrEmpty(token_movie))
                memKey = $"mirage:view:{token_movie}";

            JObject root;

            if (!hybridCache.TryGetValue(memKey, out (int category_id, JToken data) res))
            {
                string stitle = title.ToLower();

                if (memKey.Contains(":viewsearch:"))
                {
                    if (string.IsNullOrWhiteSpace(title) || year == 0)
                        return default;

                    root = await Http.Get<JObject>($"{init.apihost}/?token={init.token}&name={HttpUtility.UrlEncode(title)}&list={(serial == 1 ? "serial" : "movie")}", timeoutSeconds: 8);
                    if (root == null)
                        return (true, 0, null);

                    if (root.ContainsKey("data"))
                    {
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
                    root = await Http.Get<JObject>($"{init.apihost}/?token={init.token}&kp={kinopoisk_id}&imdb={imdb_id}&token_movie={token_movie}", timeoutSeconds: 8);
                    if (root == null)
                        return (true, 0, null);

                    if (root.ContainsKey("data"))
                    {
                        res.data = root.GetValue("data");
                        res.category_id = res.data.Value<int>("category");
                    }
                }

                if (res.data != null || (root.ContainsKey("error_info") && root.Value<string>("error_info") == "not movie"))
                    hybridCache.Set(memKey, res, cacheTime(res.category_id is 1 or 3 ? 120 : 40, init: init));
                else
                    hybridCache.Set(memKey, res, cacheTime(2, init: init));
            }

            return (false, res.category_id, res.data);
        }
        #endregion


        static string[] reffers = new string[] { "1400-princessa-i-tajna-goblinov-2024.html", "1400-princessa-i-tajna-goblinov-2024.html", "1400-princessa-i-tajna-goblinov-2024.html", "1400-princessa-i-tajna-goblinov-2024.html", "1400-princessa-i-tajna-goblinov-2024.html", "1400-princessa-i-tajna-goblinov-2024.html", "1400-princessa-i-tajna-goblinov-2024.html", "1400-princessa-i-tajna-goblinov-2024.html", "1400-princessa-i-tajna-goblinov-2024.html", "1400-princessa-i-tajna-goblinov-2024.html", "1400-princessa-i-tajna-goblinov-2024.html", "408-legenda-o-chernom-dereve-2024.html", "408-legenda-o-chernom-dereve-2024.html", "408-legenda-o-chernom-dereve-2024.html", "408-legenda-o-chernom-dereve-2024.html", "408-legenda-o-chernom-dereve-2024.html", "408-legenda-o-chernom-dereve-2024.html", "408-legenda-o-chernom-dereve-2024.html", "408-legenda-o-chernom-dereve-2024.html", "408-legenda-o-chernom-dereve-2024.html", "408-legenda-o-chernom-dereve-2024.html", "408-legenda-o-chernom-dereve-2024.html", "1221-magazin-svetilnikov-2024.html", "1221-magazin-svetilnikov-2024.html", "1221-magazin-svetilnikov-2024.html", "1221-magazin-svetilnikov-2024.html", "1221-magazin-svetilnikov-2024.html", "1221-magazin-svetilnikov-2024.html", "1221-magazin-svetilnikov-2024.html", "1221-magazin-svetilnikov-2024.html", "1221-magazin-svetilnikov-2024.html", "1221-magazin-svetilnikov-2024.html", "1221-magazin-svetilnikov-2024.html", "1112-vspylchivyj-svjaschennik-2024.html", "1112-vspylchivyj-svjaschennik-2024.html", "1112-vspylchivyj-svjaschennik-2024.html", "1112-vspylchivyj-svjaschennik-2024.html", "1112-vspylchivyj-svjaschennik-2024.html", "1112-vspylchivyj-svjaschennik-2024.html", "1112-vspylchivyj-svjaschennik-2024.html", "1112-vspylchivyj-svjaschennik-2024.html", "1112-vspylchivyj-svjaschennik-2024.html", "1112-vspylchivyj-svjaschennik-2024.html", "1112-vspylchivyj-svjaschennik-2024.html", "1239-forsazh-polnyj-vpered-2024.html", "1239-forsazh-polnyj-vpered-2024.html", "1239-forsazh-polnyj-vpered-2024.html", "1239-forsazh-polnyj-vpered-2024.html", "1239-forsazh-polnyj-vpered-2024.html", "1239-forsazh-polnyj-vpered-2024.html", "1239-forsazh-polnyj-vpered-2024.html", "1239-forsazh-polnyj-vpered-2024.html", "1239-forsazh-polnyj-vpered-2024.html", "1239-forsazh-polnyj-vpered-2024.html", "1239-forsazh-polnyj-vpered-2024.html", "1230-chelovek-vnutri-2024.html", "1230-chelovek-vnutri-2024.html", "1230-chelovek-vnutri-2024.html", "1230-chelovek-vnutri-2024.html", "1230-chelovek-vnutri-2024.html", "1230-chelovek-vnutri-2024.html", "1230-chelovek-vnutri-2024.html", "1230-chelovek-vnutri-2024.html", "1230-chelovek-vnutri-2024.html", "1230-chelovek-vnutri-2024.html", "1230-chelovek-vnutri-2024.html", "1214-moj-marchello-2024.html", "1214-moj-marchello-2024.html", "1214-moj-marchello-2024.html", "1214-moj-marchello-2024.html", "1214-moj-marchello-2024.html", "1214-moj-marchello-2024.html", "1214-moj-marchello-2024.html", "1214-moj-marchello-2024.html", "1214-moj-marchello-2024.html", "1214-moj-marchello-2024.html", "1214-moj-marchello-2024.html", "1200-reinkarnacija-vozvraschenie-vedmy-2024.html", "1200-reinkarnacija-vozvraschenie-vedmy-2024.html", "1200-reinkarnacija-vozvraschenie-vedmy-2024.html", "1200-reinkarnacija-vozvraschenie-vedmy-2024.html", "1200-reinkarnacija-vozvraschenie-vedmy-2024.html", "1200-reinkarnacija-vozvraschenie-vedmy-2024.html", "1200-reinkarnacija-vozvraschenie-vedmy-2024.html", "1200-reinkarnacija-vozvraschenie-vedmy-2024.html", "1200-reinkarnacija-vozvraschenie-vedmy-2024.html", "1200-reinkarnacija-vozvraschenie-vedmy-2024.html", "1200-reinkarnacija-vozvraschenie-vedmy-2024.html", "1185-ne-hochu-nichego-terjat-2024.html", "1185-ne-hochu-nichego-terjat-2024.html", "1185-ne-hochu-nichego-terjat-2024.html", "1185-ne-hochu-nichego-terjat-2024.html", "1185-ne-hochu-nichego-terjat-2024.html", "1185-ne-hochu-nichego-terjat-2024.html", "1185-ne-hochu-nichego-terjat-2024.html", "1185-ne-hochu-nichego-terjat-2024.html", "1185-ne-hochu-nichego-terjat-2024.html", "1185-ne-hochu-nichego-terjat-2024.html", "1185-ne-hochu-nichego-terjat-2024.html", "1168-astral-koshmar-v-spring-garden-2024.html", "1168-astral-koshmar-v-spring-garden-2024.html", "1168-astral-koshmar-v-spring-garden-2024.html", "1168-astral-koshmar-v-spring-garden-2024.html", "1168-astral-koshmar-v-spring-garden-2024.html", "1168-astral-koshmar-v-spring-garden-2024.html", "1168-astral-koshmar-v-spring-garden-2024.html", "1168-astral-koshmar-v-spring-garden-2024.html", "1168-astral-koshmar-v-spring-garden-2024.html", "1168-astral-koshmar-v-spring-garden-2024.html", "1168-astral-koshmar-v-spring-garden-2024.html", "1179-komandante-2024.html", "1179-komandante-2024.html", "1179-komandante-2024.html", "1179-komandante-2024.html", "1179-komandante-2024.html", "1179-komandante-2024.html", "1179-komandante-2024.html", "1179-komandante-2024.html", "1179-komandante-2024.html", "1179-komandante-2024.html", "1179-komandante-2024.html", "1157-bolshoe-prikljuchenie-2024.html", "1157-bolshoe-prikljuchenie-2024.html", "1157-bolshoe-prikljuchenie-2024.html", "1157-bolshoe-prikljuchenie-2024.html", "1157-bolshoe-prikljuchenie-2024.html", "1157-bolshoe-prikljuchenie-2024.html", "1157-bolshoe-prikljuchenie-2024.html", "1157-bolshoe-prikljuchenie-2024.html", "1157-bolshoe-prikljuchenie-2024.html", "1157-bolshoe-prikljuchenie-2024.html", "1157-bolshoe-prikljuchenie-2024.html", "1143-kak-stat-korolem-2024.html", "1143-kak-stat-korolem-2024.html", "1143-kak-stat-korolem-2024.html", "1143-kak-stat-korolem-2024.html", "1143-kak-stat-korolem-2024.html", "1143-kak-stat-korolem-2024.html", "1143-kak-stat-korolem-2024.html", "1143-kak-stat-korolem-2024.html", "1143-kak-stat-korolem-2024.html", "1143-kak-stat-korolem-2024.html", "1143-kak-stat-korolem-2024.html", "944-pingvin-2024.html", "944-pingвин-2024.html" };
    }
}

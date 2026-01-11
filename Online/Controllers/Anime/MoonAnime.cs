using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Engine.RxEnumerate;

namespace Online.Controllers
{
    public class MoonAnime : BaseOnlineController
    {
        static MoonAnime() {
            Directory.CreateDirectory("cache/logs/MoonAnime");
        }

        public MoonAnime() : base(AppInit.conf.MoonAnime) { }


        [HttpGet]
        [Route("lite/moonanime")]
        async public Task<ActionResult> Index(string imdb_id, string title, string original_title, long animeid, string t, int s = -1, bool rjson = false, bool similar = false)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.token))
                return OnError("token", statusCode: 401, gbcache: false);

            if (animeid == 0)
            {
                #region Поиск
                return await InvkSemaphore($"moonanime:search:{imdb_id}:{title}:{original_title}", async key =>
                {
                    if (!hybridCache.TryGetValue(key, out List<(string title, string year, long id, string poster)> catalog, inmemory: false))
                    {
                        async Task<JObject> goSearch(string arg)
                        {
                            if (string.IsNullOrEmpty(arg.Split("=")?[1]))
                                return null;

                            var search = await httpHydra.Get<JObject>($"{init.corsHost()}/api/2.0/titles?api_key={init.token}&limit=20" + arg, safety: true);
                            if (search == null || !search.ContainsKey("anime_list"))
                                return null;

                            if (search["anime_list"].Count() == 0)
                                return null;

                            return search;
                        }

                        JObject search = await goSearch($"&imdbid={imdb_id}") 
                            ?? await goSearch($"&japanese_title={HttpUtility.UrlEncode(original_title)}") 
                            ?? await goSearch($"&title={HttpUtility.UrlEncode(title)}");

                        if (search == null)
                            return OnError(refresh_proxy: true);

                        catalog = new List<(string title, string year, long id, string poster)>();

                        foreach (var anime in search["anime_list"])
                        {
                            string _titl = anime.Value<string>("title");
                            int year = anime.Value<int>("year");

                            if (string.IsNullOrEmpty(_titl))
                                continue;

                            catalog.Add((_titl, year.ToString(), anime.Value<long>("id"), anime.Value<string>("poster")));
                        }

                        if (catalog.Count == 0)
                            return OnError();

                        proxyManager?.Success();
                        hybridCache.Set(key, catalog, cacheTime(40), inmemory: false);
                    }

                    if (!similar && catalog.Count == 1)
                        return LocalRedirect(accsArgs($"/lite/moonanime?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&animeid={catalog[0].id}"));

                    var stpl = new SimilarTpl(catalog.Count);

                    foreach (var res in catalog)
                    {
                        string uri = $"{host}/lite/moonanime?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&animeid={res.id}";
                        stpl.Append(res.title, res.year, string.Empty, uri, PosterApi.Size(res.poster));
                    }

                    return await ContentTpl(stpl);
                });
                #endregion
            }
            else 
            {
                #region Серии
                return await InvkSemaphore($"moonanime:playlist:{animeid}", async key =>
                {
                    if (!hybridCache.TryGetValue(key, out JArray root))
                    {
                        root = await httpHydra.Get<JArray>($"{init.corsHost()}/api/2.0/title/{animeid}/videos?api_key={init.token}", safety: true);
                        if (root == null)
                            return OnError(refresh_proxy: true);

                        proxyManager?.Success();
                        hybridCache.Set(key, root, cacheTime(30));
                    }

                    if (s == -1)
                    {
                        var tpl = new SeasonTpl();
                        var temp = new HashSet<string>();

                        foreach (var voices in root)
                        {
                            foreach (var voice in voices.ToObject<Dictionary<string, Dictionary<string, JArray>>>())
                            {
                                foreach (var season in voice.Value)
                                {
                                    if (temp.Contains(season.Key))
                                        continue;

                                    temp.Add(season.Key);

                                    tpl.Append($"{season.Key} сезон", $"{host}/lite/moonanime?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&animeid={animeid}&s={season.Key}", season.Key);
                                }
                            }
                        }

                        return await ContentTpl(tpl);
                    }
                    else
                    {
                        #region Перевод
                        var vtpl = new VoiceTpl();
                        string activTranslate = t;

                        foreach (var voices in root)
                        {
                            foreach (var voice in voices.ToObject<Dictionary<string, Dictionary<string, JArray>>>())
                            {
                                foreach (var season in voice.Value)
                                {
                                    if (season.Key != s.ToString())
                                        continue;

                                    if (string.IsNullOrEmpty(activTranslate))
                                        activTranslate = voice.Key;

                                    vtpl.Append(voice.Key, activTranslate == voice.Key, $"{host}/lite/moonanime?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&animeid={animeid}&s={s}&t={HttpUtility.UrlEncode(voice.Key)}");
                                }
                            }
                        }
                        #endregion

                        var etpl = new EpisodeTpl(vtpl);
                        string sArhc = s.ToString();

                        foreach (var voices in root)
                        {
                            foreach (var voice in voices.ToObject<Dictionary<string, Dictionary<string, JArray>>>())
                            {
                                if (voice.Key != activTranslate)
                                    continue;

                                foreach (var season in voice.Value)
                                {
                                    if (season.Key != sArhc)
                                        continue;

                                    foreach (var folder in season.Value)
                                    {
                                        int episode = folder.Value<int>("episode");
                                        string vod = folder.Value<string>("vod");

                                        string link = $"{host}/lite/moonanime/video?vod={HttpUtility.UrlEncode(vod)}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}";
                                        string streamlink = accsArgs($"{link.Replace("/video", "/video.m3u8")}&play=true");

                                        etpl.Append($"{episode} серия", title, sArhc, episode.ToString(), link, "call", streamlink: streamlink);
                                    }
                                }
                            }
                        }

                        return await ContentTpl(etpl);
                    }
                });
                #endregion
            }
        }

        #region Video
        [HttpGet]
        [Route("lite/moonanime/video")]
        [Route("lite/moonanime/video.m3u8")]
        async public ValueTask<ActionResult> Video(string vod, bool play, string title, string original_title)
        {
            if (await IsRequestBlocked(rch: true, rch_check: !play))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.token))
                return OnError("token", statusCode: 401, gbcache: false);

            return await InvkSemaphore($"moonanime:vod:{vod}", async key =>
            {
                if (!hybridCache.TryGetValue(key, out (string file, string subtitle) cache))
                {
                    await httpHydra.GetSpan(vod + "?partner=lampa", useDefaultHeaders: false, addheaders: HeadersModel.Init(
                        ("cache-control", "no-cache"),
                        ("dnt", "1"),
                        ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                        ("pragma", "no-cache"),
                        ("priority", "u=0, i"),
                        ("sec-ch-ua", "\"Chromium\";v=\"130\", \"Microsoft Edge\";v=\"130\", \"Not?A_Brand\";v=\"99\""),
                        ("sec-ch-ua-mobile", "?0"),
                        ("sec-ch-ua-platform", "\"Windows\""),
                        ("sec-fetch-dest", "document"),
                        ("sec-fetch-mode", "navigate"),
                        ("sec-fetch-site", "none"),
                        ("sec-fetch-user", "?1"),
                        ("upgrade-insecure-requests", "1"),
                        ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0")
                    ), spanAction: iframe => 
                    {
                        cache.file = Rx.Match(iframe, "file: ?\"([^\"]+)\"");

                        cache.subtitle = Rx.Match(iframe, "subtitle: ?\"([^\"]+)\"");
                        if (string.IsNullOrEmpty(cache.subtitle) || cache.subtitle == "null")
                            cache.subtitle = Rx.Match(iframe, "thumbnails: ?\"([^\"]+)\"");
                    });

                    if (cache.file == null)
                        return OnError(refresh_proxy: true);

                    #region stats
                    if (rch?.enable != true)
                    {
                        _ = await Http.Post("https://moonanime.art/api/stats/", $"{{\"domain\":\"{CrypTo.DecodeBase64("bGFtcGEubXg=")}\",\"player\":\"{vod}?partner=lampa\",\"play\":1}}", timeoutSeconds: 4, httpversion: 2, removeContentType: true, useDefaultHeaders: false, proxy: proxy, headers: HeadersModel.Init(
                            ("accept", "*/*"),
                            ("cache-control", "no-cache"),
                            ("dnt", "1"),
                            ("origin", CrypTo.DecodeBase64("aHR0cDovL2xhbXBhLm14")),
                            ("pragma", "no-cache"),
                            ("priority", "u=1, i"),
                            ("referer", vod),
                            ("sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\""),
                            ("sec-ch-ua-mobile", "?0"),
                            ("sec-ch-ua-platform", "\"Windows\""),
                            ("sec-fetch-dest", "empty"),
                            ("sec-fetch-mode", "cors"),
                            ("sec-fetch-site", "same-origin"),
                            ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36")
                        ));
                    }
                    #endregion

                    proxyManager?.Success();
                    hybridCache.Set(key, cache, cacheTime(30));
                }

                var subtitles = new SubtitleTpl();
                if (!string.IsNullOrEmpty(cache.subtitle))
                    subtitles.Append("По умолчанию", cache.subtitle);

                string file = HostStreamProxy(cache.file, headers: HeadersModel.Init(
                    ("accept", "*/*"),
                    ("accept-language", "ru,en;q=0.9,en-GB;q=0.8,en-US;q=0.7"),
                    ("dnt", "1"),
                    ("origin", CrypTo.DecodeBase64("aHR0cDovL2xhbXBhLm14")),
                    ("priority", "u=1, i"),
                    ("sec-ch-ua", "\"Chromium\";v=\"130\", \"Microsoft Edge\";v=\"130\", \"Not?A_Brand\";v=\"99\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "cross-site"),
                    ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0")
                ));

                if (play)
                    return RedirectToPlay(file);

                return ContentTo(VideoTpl.ToJson("play", file, (title ?? original_title), subtitles: subtitles, vast: init.vast));
            });
        }
        #endregion
    }
}

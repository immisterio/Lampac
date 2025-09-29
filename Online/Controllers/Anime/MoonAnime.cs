using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Online.Controllers
{
    public class MoonAnime : BaseOnlineController
    {
        static MoonAnime() {
            Directory.CreateDirectory("cache/logs/MoonAnime");
        }

        ProxyManager proxyManager = new ProxyManager(AppInit.conf.MoonAnime);

        [HttpGet]
        [Route("lite/moonanime")]
        async public ValueTask<ActionResult> Index(string imdb_id, string title, string original_title, long animeid, string t, int s = -1, bool rjson = false, bool similar = false)
        {
            var init = await loadKit(AppInit.conf.MoonAnime);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.token))
                return OnError();

            if (animeid == 0)
            {
                #region Поиск
                string memkey = $"moonanime:search:{imdb_id}:{title}:{original_title}";

                return await InvkSemaphore(init, memkey, async () =>
                {
                    if (!hybridCache.TryGetValue(memkey, out List<(string title, string year, long id, string poster)> catalog, inmemory: false))
                    {
                        async Task<JObject> goSearch(string arg)
                        {
                            if (string.IsNullOrEmpty(arg.Split("=")?[1]))
                                return null;

                            var search = await Http.Get<JObject>($"{init.corsHost()}/api/2.0/titles?api_key={init.token}&limit=20" + arg, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                            if (search == null || !search.ContainsKey("anime_list"))
                                return null;

                            if (search["anime_list"].Count() == 0)
                                return null;

                            return search;
                        }

                        JObject search = await goSearch($"&imdbid={imdb_id}") ?? await goSearch($"&japanese_title={HttpUtility.UrlEncode(original_title)}") ?? await goSearch($"&title={HttpUtility.UrlEncode(title)}");
                        if (search == null)
                            return OnError(proxyManager);

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

                        proxyManager.Success();
                        hybridCache.Set(memkey, catalog, cacheTime(40, init: init), inmemory: false);
                    }

                    if (!similar && catalog.Count == 1)
                        return LocalRedirect(accsArgs($"/lite/moonanime?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&animeid={catalog[0].id}"));

                    var stpl = new SimilarTpl(catalog.Count);

                    foreach (var res in catalog)
                    {
                        string uri = $"{host}/lite/moonanime?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&animeid={res.id}";
                        stpl.Append(res.title, res.year, string.Empty, uri, PosterApi.Size(res.poster));
                    }

                    return ContentTo(rjson ? stpl.ToJson() : stpl.ToHtml());
                });
                #endregion
            }
            else 
            {
                #region Серии
                string memKey = $"moonanime:playlist:{animeid}";

                return await InvkSemaphore(init, memKey, async () =>
                {
                    if (!hybridCache.TryGetValue(memKey, out JArray root))
                    {
                        root = await Http.Get<JArray>($"{init.corsHost()}/api/2.0/title/{animeid}/videos?api_key={init.token}", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                        if (root == null)
                            return OnError(proxyManager);

                        proxyManager.Success();
                        hybridCache.Set(memKey, root, cacheTime(30, init: init));
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

                        return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
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

                        var etpl = new EpisodeTpl();
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

                        if (rjson)
                            return ContentTo(etpl.ToJson(vtpl));

                        return ContentTo(vtpl.ToHtml() + etpl.ToHtml());
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
            var init = await loadKit(AppInit.conf.MoonAnime);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.token))
                return OnError();

            string memKey = $"moonanime:vod:{vod}";

            return await InvkSemaphore(init, memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out (string file, string subtitle) cache))
                {
                    string iframe = await Http.Get(vod + "?partner=lampa", timeoutSeconds: 10, httpversion: 2, proxy: proxyManager.Get(), headers: httpHeaders(init, HeadersModel.Init(
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
                    )));

                    if (iframe == null)
                        return OnError(proxyManager);

                    cache.file = Regex.Match(iframe, "file: ?\"([^\"]+)\"").Groups[1].Value;
                    if (string.IsNullOrEmpty(cache.file))
                        return OnError();

                    #region stats
                    _ = await Http.Post("https://moonanime.art/api/stats/", $"{{\"domain\":\"{CrypTo.DecodeBase64("bGFtcGEubXg=")}\",\"player\":\"{vod}?partner=lampa\",\"play\":1}}", timeoutSeconds: 4, httpversion: 2, removeContentType: true, proxy: proxyManager.Get(), headers: HeadersModel.Init(
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

                    try
                    {
                        System.IO.File.AppendAllText($"cache/logs/MoonAnime/{DateTime.Today.ToString("MM-yyyy")}.txt", $"{DateTime.Now.ToString("dd / HH:mm")} - {requestInfo.IP} / {vod}\n");
                    }
                    catch { }
                    #endregion

                    cache.subtitle = Regex.Match(iframe, "subtitle: ?\"([^\"]+)\"").Groups[1].Value;
                    if (string.IsNullOrEmpty(cache.subtitle) || cache.subtitle == "null")
                        cache.subtitle = Regex.Match(iframe, "thumbnails: ?\"([^\"]+)\"").Groups[1].Value;

                    proxyManager.Success();
                    hybridCache.Set(memKey, cache, cacheTime(30, init: init));
                }

                var subtitles = new SubtitleTpl();
                if (!string.IsNullOrEmpty(cache.subtitle))
                    subtitles.Append("По умолчанию", cache.subtitle);

                string file = HostStreamProxy(init, cache.file, proxy: proxyManager.Get(), headers: HeadersModel.Init(
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

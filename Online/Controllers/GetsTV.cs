using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Online.Controllers
{
    public class GetsTV : BaseOnlineController
    {
        List<HeadersModel> bearer;

        public GetsTV() : base(AppInit.conf.GetsTV) 
        {
            requestInitialization = () =>
            {
                bearer = HeadersModel.Init("authorization", $"Bearer {init.token}");
            };
        }

        #region Bind
        [HttpGet]
        [AllowAnonymous]
        [Route("/lite/getstv/bind")]
        async public Task<ActionResult> Bind(string login, string pass)
        {
            string html = string.Empty;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(pass))
            {
                return ContentTo("Введите данные аккаунта getstv.com <br> <br><form method=\"get\" action=\"/lite/getstv/bind\"><input type=\"text\" name=\"login\" placeholder=\"email\"> &nbsp; &nbsp; <input type=\"text\" name=\"pass\" placeholder=\"пароль\"><br><br><button>Авторизоваться</button></form>");
            }
            else
            {
                var postdata = new System.Net.Http.StringContent($"{{\"email\":\"{login}\",\"password\":\"{pass}\",\"fingerprint\":\"{CrypTo.md5(DateTime.Now.ToString())}\",\"device\":{{}}}}", Encoding.UTF8, "application/json");
                var result =  await Http.Post<JObject>($"{init.corsHost()}/api/login", postdata, httpversion: init.httpversion, proxy: proxy, headers: httpHeaders(init));

                if (result == null)
                    return ContentTo("Ошибка авторизации ;(");

                string token = result.Value<string>("token");
                if (string.IsNullOrEmpty(token))
                    return ContentTo(JsonConvert.SerializeObject(result, Formatting.Indented));

                return ContentTo("Добавьте в init.conf<br><br>\"GetsTV\": {<br>&nbsp;&nbsp;\"enable\": true,<br>&nbsp;&nbsp;\"token\": \"" + token + "\"<br>}");
            }
        }
        #endregion

        [HttpGet]
        [Route("lite/getstv")]
        async public Task<ActionResult> Index(string orid, string title, string original_title, int year, int t = -1, int s = -1, bool rjson = false, bool similar = false, string source = null, string id = null)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(orid) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.ToLower() == "getstv")
                    orid = id;
            }

            if (string.IsNullOrEmpty(orid))
            {
                var result = await search(title, original_title, year);

                if (result.id != null && similar == false)
                    orid = result.id;
                else
                {
                    if (result.similar == null || result.similar.IsEmpty)
                        return OnError("data");

                    return await ContentTpl(result.similar);
                }
            }

            rhubFallback:
            var cache = await InvokeCacheResult<JObject>($"getstv:movies:{orid}", 20, async e =>
            {
                var root = await httpHydra.Get<JObject>($"{init.corsHost()}/api/movies/{orid}", 
                    addheaders: bearer, safety: true
                );

                if (root == null)
                    return e.Fail("movies", refresh_proxy: true);

                return e.Success(root);
            });

            if (IsRhubFallback(cache, safety: true))
                goto rhubFallback;

            return await ContentTpl(cache, () => 
            {
                string defaultargs = $"&orid={orid}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&year={year}";

                if (cache.Value.Value<string>("type") == "movie")
                {
                    #region Фильм
                    var mtpl = new MovieTpl(title, original_title);

                    foreach (var media in cache.Value["media"])
                    {
                        string link = $"{host}/lite/getstv/video.m3u8?id={media.Value<string>("_id")}";
                        string streamlink = accsArgs($"{link}&play=true");

                        mtpl.Append(media.Value<string>("trName"), link, "call", streamlink, details: media.Value<string>("sourceType"));
                    }

                    return mtpl;
                    #endregion
                }
                else
                {
                    #region Сериал
                    if (s == -1)
                    {
                        var tpl = new SeasonTpl();

                        foreach (var season in cache.Value["seasons"])
                        {
                            int seasonNum = season.Value<int>("seasonNum");
                            tpl.Append($"{seasonNum} сезон", $"{host}/lite/getstv?rjson={rjson}&s={seasonNum}{defaultargs}", seasonNum);
                        }

                        return tpl;
                    }
                    else
                    {
                        var episodes = cache.Value["seasons"].First(i => i.Value<int>("seasonNum") == s)["episodes"];

                        #region Перевод
                        var vtpl = new VoiceTpl();
                        var temp_translation = new HashSet<int>();

                        foreach (var e in episodes)
                        {
                            foreach (var tr in e["trs"])
                            {
                                int trId = tr.Value<int>("trId");
                                if (temp_translation.Contains(trId))
                                    continue;

                                temp_translation.Add(trId);

                                if (t == -1)
                                    t = trId;

                                vtpl.Append(tr.Value<string>("trName"), t == trId, $"{host}/lite/getstv?rjson={rjson}&s={s}&t={trId}{defaultargs}");
                            }
                        }
                        #endregion

                        var etpl = new EpisodeTpl(vtpl);

                        foreach (var episode in episodes)
                        {
                            foreach (var tr in episode["trs"])
                            {
                                if (tr.Value<int>("trId") == t)
                                {
                                    int e = episode.Value<int>("episodeNum");
                                    string link = $"{host}/lite/getstv/video.m3u8?id={tr.Value<string>("_id")}";
                                    string streamlink = accsArgs($"{link}&play=true");

                                    etpl.Append($"{e} серия", title ?? original_title, s.ToString(), e.ToString(), link, "call", streamlink: streamlink);
                                    break;
                                }
                            }
                        }

                        return etpl;
                    }
                    #endregion
                }
            });
        }

        #region Video
        [HttpGet]
        [Route("lite/getstv/video.m3u8")]
        async public ValueTask<ActionResult> Video(string id, bool play)
        {
            if (await IsRequestBlocked(rch: true, rch_check: !play))
                return badInitMsg;

            return await InvkSemaphore($"getstv:view:stream:{id}:{init.token}", async key =>
            {
                if (!hybridCache.TryGetValue(key, out JObject root))
                {
                    root = await httpHydra.Get<JObject>($"{init.corsHost()}/api/media/{id}?format=m3u8&protocol=https", 
                        addheaders: bearer, safety: true
                    );

                    if (root == null)
                        return OnError("json", refresh_proxy: true);

                    if (!root.ContainsKey("resolutions"))
                        return OnError("resolutions");

                    proxyManager?.Success();
                    hybridCache.Set(key, root, cacheTime(10));
                }

                #region subtitle
                var subtitles = new SubtitleTpl();

                try
                {
                    foreach (var sub in root["subtitles"])
                        subtitles.Append(sub.Value<string>("lang"), sub.Value<string>("url"));
                }
                catch { }
                #endregion

                var streamquality = new StreamQualityTpl();

                foreach (var r in root["resolutions"])
                    streamquality.Append(HostStreamProxy(r.Value<string>("url")), $"{r.Value<int>("type")}p");

                if (!streamquality.Any())
                    return OnError("stream");

                if (play)
                    return RedirectToPlay(streamquality.Firts().link);

                var titleObj = root["media"]["movie"]["title"] as JObject;
                string titleRu = titleObj?["ru"]?.ToString();
                string titleEn = titleObj?["en"]?.ToString();

                string name = titleRu ?? titleEn;
                if (titleRu != null && titleEn != null)
                    name = $"{titleRu} / {titleEn}";

                return ContentTo(VideoTpl.ToJson("play", streamquality.Firts().link, name,
                    streamquality: streamquality,
                    vast: init.vast,
                    subtitles: subtitles
                ));
            });
        }
        #endregion

        #region SpiderSearch
        [HttpGet]
        [Route("lite/getstv-search")]
        async public Task<ActionResult> SpiderSearch(string title, bool rjson = false)
        {
            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            var result = await search(title, null, 0);
            if (result.similar == null || result.similar.IsEmpty)
                return OnError("data");

            return await ContentTpl(result.similar);
        }
        #endregion


        #region search
        async ValueTask<(string id, SimilarTpl similar)> search(string title, string original_title, int year)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrEmpty(init.token))
                return default;

            string memKey = $"getstv:search:{title ?? original_title}";
            if (!hybridCache.TryGetValue(memKey, out JArray root))
            {
                root = await httpHydra.Get<JArray>($"{init.corsHost()}/api/movies?skip=0&sort=updated&searchText={HttpUtility.UrlEncode(title)}", 
                    addheaders: bearer, safety: true
                );
                
                if (root == null)
                {
                    proxyManager?.Refresh();
                    return default;
                }

                proxyManager?.Success();
                hybridCache.Set(memKey, root, cacheTime(20));
            }

            List<string> ids = new List<string>();
            var stpl = new SimilarTpl(root.Count);

            string stitle = StringConvert.SearchName(title);
            string soriginal_title = StringConvert.SearchName(original_title);

            foreach (var j in root)
            {
                string uri = $"{host}/lite/getstv?orid={j.Value<string>("_id")}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&year={year}";

                var titleObj = j["title"] as JObject;
                string titleRu = titleObj?["ru"]?.ToString();
                string titleEn = titleObj?["en"]?.ToString();

                string name = titleRu ?? titleEn;
                if (titleRu != null && titleEn != null)
                    name = $"{titleRu} / {titleEn}";

                int released = j.Value<DateTime>("released").Year;
                string img = $"https://img.getstv.com/poster/cover/345x518/{j.Value<string>("poster")}.jpg";
                stpl.Append(name, released.ToString(), j.Value<string>("contentType"), uri, img);

                if ((titleRu != null && (StringConvert.SearchName(titleRu) == stitle || StringConvert.SearchName(titleRu) == soriginal_title)) ||
                    (titleEn != null && (StringConvert.SearchName(titleEn) == stitle || StringConvert.SearchName(titleEn) == soriginal_title)))
                {
                    if (released == year)
                        ids.Add(j.Value<string>("_id"));
                }
            }

            if (ids.Count == 1)
                return (ids[0], stpl);

            return (null, stpl);
        }
        #endregion
    }
}

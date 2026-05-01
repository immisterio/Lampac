using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Attributes;
using System.Net.Http;
using System.Text;
using Shared;
using Shared.Models.Templates;
using Shared.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Shared.Models.Base;
using Shared.Services.Utilities;

namespace GetsTV;

public class GetsTVController : BaseOnlineController
{
    List<HeadersModel> bearer;
    static readonly HttpClient httpClient = FriendlyHttp.CreateHttpClient();

    public GetsTVController() : base(ModInit.conf)
    {
        requestInitialization += () =>
        {
            if (init.httpversion == 1)
                httpHydra.RegisterHttp(httpClient);
        };

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
        if (!requestInfo.IsLocalIp)
            return ContentTo("is not local ip");

        string html = string.Empty;

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(pass))
        {
            return ContentTo("Введите данные аккаунта getstv.com <br> <br><form method=\"get\" action=\"/lite/getstv/bind\"><input type=\"text\" name=\"login\" placeholder=\"email\"> &nbsp; &nbsp; <input type=\"text\" name=\"pass\" placeholder=\"пароль\"><br><br><button>Авторизоваться</button></form>");
        }
        else
        {
            var postdata = new System.Net.Http.StringContent($"{{\"email\":\"{login}\",\"password\":\"{pass}\",\"fingerprint\":\"{CrypTo.md5(DateTime.Now.ToString())}\",\"device\":{{}}}}", Encoding.UTF8, "application/json");
            var result = await Http.Post<JObject>($"{init.host}/api/login", postdata, httpversion: init.httpversion, proxy: proxy, headers: httpHeaders(init));

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
    [Staticache]
    [Route("lite/getstv")]
    async public Task<ActionResult> Index(string orid, string title, string original_title, int year, int t = -1, int s = -1, bool rjson = false, bool similar = false, string source = null, string id = null)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        if (string.IsNullOrEmpty(orid) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
        {
            if (source.Equals("getstv", StringComparison.OrdinalIgnoreCase))
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
                    return OnError("similar");

                return ContentTpl(result.similar);
            }
        }

    rhubFallback:
        var cache = await InvokeCacheResult<MovieDetailsRoot>($"getstv:movies:{orid}", 40, async e =>
        {
            var root = await httpHydra.Get<MovieDetailsRoot>($"{init.host}/api/movies/{orid}",
                addheaders: bearer,
                safety: true
            );

            if (root == null)
                return e.Fail("movies", refresh_proxy: true);

            return e.Success(root);
        });

        if (IsRhubFallback(cache, safety: true))
            goto rhubFallback;

        return ContentTpl(cache, () =>
        {
            string defaultargs = $"&orid={orid}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&year={year}";

            if (cache.Value.type == "movie")
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);

                foreach (var media in cache.Value.media)
                {
                    string link = $"{host}/lite/getstv/video.m3u8?id={media._id}";
                    mtpl.Append(
                        media.trName,
                        link,
                        "call",
                        accsArgs($"{link}&play=true"),
                        details: media.sourceType
                    );
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

                    foreach (var season in cache.Value.seasons)
                    {
                        int seasonNum = season.seasonNum;

                        tpl.Append(
                            $"{seasonNum} сезон",
                            $"{host}/lite/getstv?rjson={rjson}&s={seasonNum}{defaultargs}",
                            seasonNum
                        );
                    }

                    return tpl;
                }
                else
                {
                    var episodes = cache.Value.seasons.First(i => i.seasonNum == s).episodes;

                    #region Перевод
                    var vtpl = new VoiceTpl();
                    var temp_translation = new HashSet<int>();

                    foreach (var e in episodes)
                    {
                        foreach (var tr in e.trs)
                        {
                            int trId = tr.trId;
                            if (temp_translation.Contains(trId))
                                continue;

                            temp_translation.Add(trId);

                            if (t == -1)
                                t = trId;

                            vtpl.Append(
                                tr.trName,
                                t == trId,
                                $"{host}/lite/getstv?rjson={rjson}&s={s}&t={trId}{defaultargs}"
                            );
                        }
                    }
                    #endregion

                    var etpl = new EpisodeTpl(vtpl);

                    foreach (var episode in episodes)
                    {
                        foreach (var tr in episode.trs)
                        {
                            if (tr.trId == t)
                            {
                                int e = episode.episodeNum;
                                string link = $"{host}/lite/getstv/video.m3u8?id={tr._id}";
                                string streamlink = accsArgs($"{link}&play=true");

                                etpl.Append(
                                    $"{e} серия",
                                    title ?? original_title,
                                    s.ToString(),
                                    e.ToString(),
                                    link,
                                    "call",
                                    streamlink: streamlink
                                );
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
    async public Task<ActionResult> Video(string id, bool play)
    {
        if (await IsRequestBlocked(rch: true, rch_check: !play))
            return badInitMsg;

        rhubFallback:
        var cache = await InvokeCacheResult<MediaStreamRoot>($"getstv:view:stream:{id}:{init.token}", 10, async e =>
        {
            var root = await httpHydra.Get<MediaStreamRoot>($"{init.host}/api/media/{id}?format=m3u8&protocol=https",
                addheaders: bearer,
                safety: true
            );

            if (root == null)
                return e.Fail("json", refresh_proxy: true);

            if (root?.resolutions == null || root.resolutions.Count == 0)
                return e.Fail("resolutions");

            return e.Success(root);
        });

        if (IsRhubFallback(cache, safety: true))
            goto rhubFallback;

        if (!cache.IsSuccess)
            return OnError(cache.ErrorMsg);

        var root = cache.Value;

        #region subtitle
        var subtitles = new SubtitleTpl();

        if (root.subtitles != null)
        {
            foreach (var sub in root.subtitles)
                subtitles.Append(sub.lang, sub.url);
        }
        #endregion

        var streamquality = new StreamQualityTpl();

        foreach (var r in root.resolutions)
            streamquality.Append(HostStreamProxy(r.url), $"{r.type}p");

        var first = streamquality.Firts();
        if (first == null)
            return OnError("stream");

        if (play)
            return RedirectToPlay(first.link);

        string titleRu = root.media?.movie?.title?.ru;
        string titleEn = root.media?.movie?.title?.en;

        string name = titleRu ?? titleEn;
        if (titleRu != null && titleEn != null)
            name = $"{titleRu} / {titleEn}";

        return ContentTo(VideoTpl.ToJson(
            "play",
            first.link,
            name,
            streamquality: streamquality,
            vast: init.vast,
            subtitles: subtitles,
            httpContext: HttpContext
        ));
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

        return ContentTpl(result.similar);
    }
    #endregion


    #region search
    async ValueTask<(string id, SimilarTpl similar)> search(string title, string original_title, int year)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrEmpty(init.token))
            return default;

        List<SearchItem> root = null;

        string memKey = $"getstv:search:{title}";
        var entryCache = await hybridCache.EntryAsync<List<SearchItem>>(memKey);
        if (entryCache.success)
        {
            root = entryCache.value;
        }
        else
        {
            root = await httpHydra.Get<List<SearchItem>>($"{init.host}/api/movies?skip=0&sort=updated&searchText={HttpUtility.UrlEncode(title)}",
                addheaders: bearer,
                safety: true
            );

            if (root == null)
            {
                proxyManager?.Refresh();
                return default;
            }

            proxyManager?.Success();
            hybridCache.Set(memKey, root, TimeSpan.FromHours(4));
        }

        List<string> ids = new List<string>();
        var stpl = new SimilarTpl(root.Count);

        string enc_title = HttpUtility.UrlEncode(title);
        string enc_original_title = HttpUtility.UrlEncode(original_title);

        string stitle = StringConvert.SearchName(title);
        string soriginal_title = StringConvert.SearchName(original_title);

        foreach (var j in root)
        {
            string uri = $"{host}/lite/getstv?orid={j._id}&title={enc_title}&original_title={enc_original_title}&year={year}";

            string titleRu = j.title?.ru;
            string titleEn = j.title?.en;

            string name = titleRu ?? titleEn;
            if (titleRu != null && titleEn != null)
                name = $"{titleRu} / {titleEn}";

            int released = j.released.Year;
            string img = $"https://img.getstv.com/poster/cover/345x518/{j.poster}.jpg";
            stpl.Append(
                name,
                released.ToString(),
                j.contentType,
                uri,
                PosterApi.Size(img)
            );

            if ((titleRu != null && (StringConvert.SearchName(titleRu) == stitle || StringConvert.SearchName(titleRu) == soriginal_title)) ||
                (titleEn != null && (StringConvert.SearchName(titleEn) == stitle || StringConvert.SearchName(titleEn) == soriginal_title)))
            {
                if (released == year)
                    ids.Add(j._id);
            }
        }

        if (ids.Count == 1)
            return (ids[0], stpl);

        return (null, stpl);
    }
    #endregion
}

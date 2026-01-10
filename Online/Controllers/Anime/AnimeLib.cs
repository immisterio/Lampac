using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Engine.Utilities;
using Shared.Models.Online.AnimeLib;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace Online.Controllers
{
    public class AnimeLib : BaseOnlineController
    {
        public AnimeLib() : base(AppInit.conf.AnimeLib) { }

        static readonly SemaphoreSlim TokenSemaphore = new SemaphoreSlim(1, 1);

        [HttpGet]
        [Route("lite/animelib")]
        async public Task<ActionResult> Index(string title, string original_title, int year, string uri, string t, bool rjson = false, bool similar = false)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            await EnsureAnimeLibToken();

            if (string.IsNullOrEmpty(init.token))
                return OnError("token", statusCode: 401, gbcache: false);

            var bearer = HeadersModel.Init("authorization", $"Bearer {init.token}");

            if (string.IsNullOrWhiteSpace(uri))
            {
                #region Поиск
                if (string.IsNullOrWhiteSpace(title))
                    return OnError();

                return await InvkSemaphore($"animelib:search:{title}:{original_title}", async key =>
                {
                    if (!hybridCache.TryGetValue(key, out List<(string title, string year, string uri, bool coincidence, string cover)> catalog, inmemory: false))
                    {
                        async Task<DataSearch[]> goSearch(string q)
                        {
                            if (string.IsNullOrEmpty(q))
                                return null;

                            string req_uri = $"{init.corsHost()}/api/anime?fields[]=rate_avg&fields[]=rate&fields[]=releaseDate&q={HttpUtility.UrlEncode(q)}";

                            var result = await httpHydra.Get<JObject>(req_uri, addheaders: bearer, safety: true);

                            if (result == null || !result.ContainsKey("data"))
                                return null;

                            return result["data"].ToObject<DataSearch[]>();
                        }

                        var search = await goSearch(original_title);

                        if (search == null || search.Length == 0)
                            search = await goSearch(title);

                        if (search == null || search.Length == 0)
                            return OnError(refresh_proxy: true);

                        string stitle = StringConvert.SearchName(title);
                        catalog = new List<(string title, string year, string uri, bool coincidence, string cover)>(search.Length);

                        foreach (var anime in search)
                        {
                            if (string.IsNullOrEmpty(anime.slug_url))
                                continue;

                            var model = ($"{anime.rus_name} / {anime.eng_name}", (anime.releaseDate != null ? anime.releaseDate.Split("-")[0] : "0"), anime.slug_url, false, anime.cover.@default);

                            if (stitle == StringConvert.SearchName(anime.rus_name) || stitle == StringConvert.SearchName(anime.eng_name))
                            {
                                if (!string.IsNullOrEmpty(anime.releaseDate) && anime.releaseDate.StartsWith(year.ToString()))
                                    model.Item4 = true;
                            }

                            catalog.Add(model);
                        }

                        if (catalog.Count == 0)
                            return OnError();

                        proxyManager?.Success();

                        hybridCache.Set(key, catalog, cacheTime(40), inmemory: false);
                    }

                    if (!similar && catalog.Where(i => i.coincidence).Count() == 1)
                        return LocalRedirect(accsArgs($"/lite/animelib?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(catalog.First(i => i.coincidence).uri)}"));

                    var stpl = new SimilarTpl(catalog.Count);

                    foreach (var res in catalog)
                        stpl.Append(res.title, res.year, string.Empty, $"{host}/lite/animelib?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(res.uri)}", PosterApi.Size(res.cover));

                    return await ContentTpl(stpl);
                });
                #endregion
            }
            else
            {
                #region Серии
                return await InvkSemaphore($"animelib:playlist:{uri}", async key =>
                {
                    if (!hybridCache.TryGetValue(key, out Episode[] episodes))
                    {
                        string req_uri = $"{init.corsHost()}/api/episodes?anime_id={uri}";

                        var root = await httpHydra.Get<JObject>(req_uri, addheaders: bearer, safety: true);

                        if (root == null || !root.ContainsKey("data"))
                            return OnError(refresh_proxy: true);

                        episodes = root["data"].ToObject<Episode[]>();

                        if (episodes.Length == 0)
                            return OnError();

                        proxyManager?.Success();

                        hybridCache.Set(key, episodes, cacheTime(30));
                    }

                    #region Перевод
                    string voice_memkey = $"animelib:video:{episodes.First().id}";
                    if (!hybridCache.TryGetValue(voice_memkey, out Player[] players))
                    {
                        if (rch != null && rch.IsNotConnected())
                            return ContentTo(rch.connectionMsg);

                        string req_uri = $"{init.corsHost()}/api/episodes/{episodes.First().id}";

                        var root = await httpHydra.Get<JObject>(req_uri, addheaders: bearer, safety: true);

                        if (root == null || !root.ContainsKey("data"))
                            return OnError(refresh_proxy: true);

                        players = root["data"]["players"].ToObject<Player[]>();
                        hybridCache.Set(voice_memkey, players, cacheTime(30));
                    }

                    var vtpl = new VoiceTpl(players.Length);
                    string activTranslate = t;

                    foreach (var player in players)
                    {
                        if (player.player != "Animelib")
                            continue;

                        if (string.IsNullOrEmpty(activTranslate))
                            activTranslate = player.team.name;

                        vtpl.Append(player.team.name, activTranslate == player.team.name, $"{host}/lite/animelib?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(uri)}&t={HttpUtility.UrlEncode(player.team.name)}");
                    }
                    #endregion

                    var etpl = new EpisodeTpl(vtpl, episodes.Length);

                    foreach (var episode in episodes)
                    {
                        string name = string.IsNullOrEmpty(episode.name) ? title : $"{title} / {episode.name}";

                        string link = $"{host}/lite/animelib/video?id={episode.id}&voice={HttpUtility.UrlEncode(activTranslate)}&title={HttpUtility.UrlEncode(title)}";

                        etpl.Append($"{episode.number} серия", name, episode.season, episode.number, link, "call", streamlink: accsArgs($"{link}&play=true"));
                    }

                    return await ContentTpl(etpl);
                });
                #endregion
            }
        }

        #region Video
        [HttpGet]
        [Route("lite/animelib/video")]
        async public ValueTask<ActionResult> Video(string title, long id, string voice, bool play)
        {
            if (await IsRequestBlocked(rch: true, rch_check: false))
                return badInitMsg;

            await EnsureAnimeLibToken();

            if (string.IsNullOrEmpty(init.token))
                return OnError();

            if (rch != null)
            {
                if (rch.IsNotConnected())
                {
                    if (init.rhub_fallback && play)
                        rch.Disabled();
                    else
                        return ContentTo(rch.connectionMsg);
                }

                if (!play && rch.IsRequiredConnected())
                    return ContentTo(rch.connectionMsg);
            }

            rhubFallback:
            var cache = await InvokeCacheResult<Player[]>($"animelib:video:{id}", 30, async e =>
            {
                string req_uri = $"{init.corsHost()}/api/episodes/{id}";
                var bearer = HeadersModel.Init("authorization", $"Bearer {init.token}");

                var root = await httpHydra.Get<JObject>(req_uri, addheaders: bearer, safety: true);

                if (root == null || !root.ContainsKey("data"))
                    return e.Fail("data", refresh_proxy: true);

                return e.Success(root["data"]["players"].ToObject<Player[]>());
            });

            if (IsRhubFallback(cache, safety: true))
                goto rhubFallback;

            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            var headers_stream = httpHeaders(init.host, init.headers_stream);

            IReadOnlyList<StreamQualityDto> streams;

            if (string.IsNullOrEmpty(voice))
            {
                streams = goStreams(cache.Value, null, headers_stream);
            }
            else
            {
                streams = goStreams(cache.Value, voice, headers_stream);
                if (streams.Count == 0)
                    streams = goStreams(cache.Value, null, headers_stream);
            }

            if (streams == null || streams.Count == 0)
                return OnError("streams");

            var streamquality = new StreamQualityTpl(streams);

            if (play)
                return RedirectToPlay(streamquality.Firts().link);

            return ContentTo(VideoTpl.ToJson("play", streamquality.Firts().link, title, streamquality: streamquality, vast: init.vast, headers: init.streamproxy ? null : headers_stream));
        }
        #endregion


        #region goStreams
        IReadOnlyList<StreamQualityDto> goStreams(in Player[] players, string _voice, List<HeadersModel> headers_stream)
        {
            var _streams = new List<StreamQualityDto>(20);

            foreach (var player in players)
            {
                if (player.player != "Animelib")
                    continue;

                if (!string.IsNullOrEmpty(_voice) && _voice != player.team.name)
                    continue;

                foreach (var video in player.video.quality)
                {
                    if (string.IsNullOrEmpty(video.href))
                        continue;

                    string file = HostStreamProxy("https://video1.cdnlibs.org/.%D0%B0s/" + video.href, headers: headers_stream);

                    _streams.Add(new StreamQualityDto(file, $"{video.quality}p"));
                }

                if (_streams.Count > 0)
                    break;
            }

            return _streams;
        }
        #endregion

        #region [Codex AI] EnsureAnimeLibToken / RequestAnimeLibToken
        async ValueTask EnsureAnimeLibToken()
        {
            if (!string.IsNullOrEmpty(init.token))
                return;

            try
            {
                await TokenSemaphore.WaitAsync(TimeSpan.FromMinutes(1));

                AnimeLibTokenState cache = null;
                string TokenCachePath = Path.Combine("cache", "animelib.json");

                try
                {
                    string json = System.IO.File.ReadAllText(TokenCachePath);
                    cache = JsonConvert.DeserializeObject<AnimeLibTokenState>(json);
                }
                catch { }

                if (cache == null)
                    return;

                if (!string.IsNullOrEmpty(cache.token) && cache.refresh_time > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    init.token = cache.token;
                    return;
                }

                var tokens = await RequestAnimeLibToken(cache.refresh_token);
                if (tokens == null)
                    return;

                cache = new AnimeLibTokenState
                {
                    token = tokens.Value.accessToken,
                    refresh_token = tokens.Value.refreshToken,
                    // 2592000 секунд / 60 = 43200 минут
                    // 43200 минут / 60 = 720 часов
                    // 720 часов / 24 = 30 дней
                    refresh_time = DateTimeOffset.UtcNow.AddDays(20).ToUnixTimeSeconds()
                };

                try
                {
                    System.IO.File.WriteAllText(TokenCachePath, JsonConvertPool.SerializeObject(cache));
                }
                catch { }

                init.token = cache.token;
            }
            catch { }
            finally
            {
                TokenSemaphore.Release();
            }
        }

        async ValueTask<(string accessToken, string refreshToken)?> RequestAnimeLibToken(string refreshToken)
        {
            var payload = JsonConvertPool.SerializeObject(new
            {
                grant_type = "refresh_token",
                client_id = "1",
                refresh_token = refreshToken,
                scope = string.Empty
            });

            using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
            {
                var headers = HeadersModel.Init(Http.defaultFullHeaders,
                    ("accept", "*/*"),
                    ("origin", "https://anilib.me"),
                    ("referer", "https://anilib.me/"),
                    ("accept-language", "en-US,en;q=0.9,ru;q=0.8"),
                    ("client-time-zone", "Europe/Kiev"),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "cross-site"),
                    ("site-id", "5")
                );

                var result = await Http.Post<JObject>("https://api.cdnlibs.org/api/auth/oauth/token", content, 
                    httpversion: init.httpversion, timeoutSeconds: init.httptimeout, headers: headers, useDefaultHeaders: false
                );

                if (result == null)
                    return null;

                //{"token_type":"Bearer","expires_in":2592000,"access_token":"*","refresh_token":"*"}

                string accessToken = result.Value<string>("access_token");
                string newRefreshToken = result.Value<string>("refresh_token");

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(newRefreshToken))
                    return null;

                return (accessToken, newRefreshToken);
            }
        }
        #endregion
    }
}

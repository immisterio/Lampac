using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.AnimeLib;
using Shared.Models.Online.Settings;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace Online.Controllers
{
    public class AnimeLib : BaseOnlineController
    {
        private static readonly SemaphoreSlim TokenSemaphore = new SemaphoreSlim(1, 1);

        ProxyManager proxyManager = new ProxyManager(AppInit.conf.AnimeLib);

        [HttpGet]
        [Route("lite/animelib")]
        async public ValueTask<ActionResult> Index(string title, string original_title, int year, string uri, string t, bool rjson = false, bool similar = false)
        {
            var init = await loadKit(AppInit.conf.AnimeLib);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            await EnsureAnimeLibToken(init);

            if (string.IsNullOrEmpty(init.token))
                return OnError();

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            var headers = httpHeaders(init, HeadersModel.Init("authorization", $"Bearer {init.token}"));

            if (string.IsNullOrWhiteSpace(uri))
            {
                #region Поиск
                if (string.IsNullOrWhiteSpace(title))
                    return OnError();

                string memkey = $"animelib:search:{title}:{original_title}";

                return await InvkSemaphore(init, memkey, async () =>
                {
                    if (!hybridCache.TryGetValue(memkey, out List<(string title, string year, string uri, bool coincidence, string cover)> catalog, inmemory: false))
                    {
                        async Task<DataSearch[]> goSearch(string q)
                        {
                            if (string.IsNullOrEmpty(q))
                                return null;

                            string req_uri = $"{init.corsHost()}/api/anime?fields[]=rate_avg&fields[]=rate&fields[]=releaseDate&q={HttpUtility.UrlEncode(q)}";
                            var result = rch.enable ? await rch.Get<JObject>(req_uri, headers) :
                                                      await Http.Get<JObject>(req_uri, httpversion: 2, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: headers);

                            if (result == null || !result.ContainsKey("data"))
                                return null;

                            return result["data"].ToObject<DataSearch[]>();
                        }

                        var search = await goSearch(original_title);

                        if (search == null || search.Length == 0)
                            search = await goSearch(title);

                        if (search == null || search.Length == 0)
                            return OnError(proxyManager, refresh_proxy: !rch.enable);

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

                        if (!rch.enable)
                            proxyManager.Success();

                        hybridCache.Set(memkey, catalog, cacheTime(40, init: init), inmemory: false);
                    }

                    if (!similar && catalog.Where(i => i.coincidence).Count() == 1)
                        return LocalRedirect(accsArgs($"/lite/animelib?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(catalog.First(i => i.coincidence).uri)}"));

                    var stpl = new SimilarTpl(catalog.Count);

                    foreach (var res in catalog)
                        stpl.Append(res.title, res.year, string.Empty, $"{host}/lite/animelib?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(res.uri)}", PosterApi.Size(res.cover));

                    return ContentTo(rjson ? stpl.ToJson() : stpl.ToHtml());
                });
                #endregion
            }
            else
            {
                #region Серии
                string memKey = $"animelib:playlist:{uri}";

                return await InvkSemaphore(init, memKey, async () =>
                {
                    if (!hybridCache.TryGetValue(memKey, out Episode[] episodes))
                    {
                        string req_uri = $"{init.corsHost()}/api/episodes?anime_id={uri}";

                        var root = rch.enable ? await rch.Get<JObject>(req_uri, headers) :
                                                await Http.Get<JObject>(req_uri, timeoutSeconds: 8, httpversion: 2, proxy: proxyManager.Get(), headers: headers);

                        if (root == null || !root.ContainsKey("data"))
                            return OnError(proxyManager, refresh_proxy: !rch.enable);

                        episodes = root["data"].ToObject<Episode[]>();

                        if (episodes.Length == 0)
                            return OnError();

                        if (!rch.enable)
                            proxyManager.Success();

                        hybridCache.Set(memKey, episodes, cacheTime(30, init: init));
                    }

                    #region Перевод
                    memKey = $"animelib:video:{episodes.First().id}";
                    if (!hybridCache.TryGetValue(memKey, out Player[] players))
                    {
                        if (rch.IsNotConnected())
                            return ContentTo(rch.connectionMsg);

                        string req_uri = $"{init.corsHost()}/api/episodes/{episodes.First().id}";

                        var root = rch.enable ? await rch.Get<JObject>(req_uri, headers) :
                                                await Http.Get<JObject>(req_uri, httpversion: 2, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: headers);

                        if (root == null || !root.ContainsKey("data"))
                            return OnError(proxyManager, refresh_proxy: !rch.enable);

                        players = root["data"]["players"].ToObject<Player[]>();
                        hybridCache.Set(memKey, players, cacheTime(30, init: init));
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

                    var etpl = new EpisodeTpl(episodes.Length);

                    foreach (var episode in episodes)
                    {
                        string name = string.IsNullOrEmpty(episode.name) ? title : $"{title} / {episode.name}";

                        string link = $"{host}/lite/animelib/video?id={episode.id}&voice={HttpUtility.UrlEncode(activTranslate)}&title={HttpUtility.UrlEncode(title)}";

                        etpl.Append($"{episode.number} серия", name, episode.season, episode.number, link, "call", streamlink: accsArgs($"{link}&play=true"));
                    }

                    if (rjson)
                        return ContentTo(etpl.ToJson(vtpl));

                    return ContentTo(vtpl.ToHtml() + etpl.ToHtml());
                });
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/animelib/video")]
        async public ValueTask<ActionResult> Video(string title, long id, string voice, bool play)
        {
            var init = await loadKit(AppInit.conf.AnimeLib);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            await EnsureAnimeLibToken(init);

            if (string.IsNullOrEmpty(init.token))
                return OnError();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotConnected() && init.rhub_fallback && play)
                rch.Disabled();

            var cache = await InvokeCache<Player[]>($"animelib:video:{id}", cacheTime(30, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string req_uri = $"{init.corsHost()}/api/episodes/{id}";
                var headers = httpHeaders(init, HeadersModel.Init("authorization", $"Bearer {init.token}"));

                var root = rch.enable ? await rch.Get<JObject>(req_uri, headers) :
                                        await Http.Get<JObject>(req_uri, httpversion: 2, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: headers);

                if (root == null || !root.ContainsKey("data"))
                    return res.Fail("data");

                return root["data"]["players"].ToObject<Player[]>();
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            var headers_stream = httpHeaders(init.host, init.headers_stream);

            #region goStreams
            List<(string link, string quality)> goStreams(string _voice)
            {
                var _streams = new List<(string link, string quality)>(5);

                foreach (var player in cache.Value)
                {
                    if (player.player != "Animelib")
                        continue;

                    if (!string.IsNullOrEmpty(_voice) && _voice != player.team.name)
                        continue;

                    foreach (var video in player.video.quality)
                    {
                        if (string.IsNullOrEmpty(video.href))
                            continue;

                        string file = HostStreamProxy(init, "https://video1.cdnlibs.org/.%D0%B0s/" + video.href, proxy: proxyManager.Get(), headers: headers_stream);

                        _streams.Add((file, $"{video.quality}p"));
                    }

                    if (_streams.Count > 0)
                        break;
                }

                return _streams;
            }
            #endregion

            List<(string link, string quality)> streams;

            if (string.IsNullOrEmpty(voice))
            {
                streams = goStreams(null);
            }
            else
            {
                streams = goStreams(voice);
                if (streams.Count == 0)
                    streams = goStreams(null);
            }

            if (streams == null || streams.Count == 0)
                return OnError("streams");

            var streamquality = new StreamQualityTpl(streams);

            if (play)
                return RedirectToPlay(streamquality.Firts().link);

            return ContentTo(VideoTpl.ToJson("play", streamquality.Firts().link, title, streamquality: streamquality, vast: init.vast, headers: init.streamproxy ? null : headers_stream));
        }
        #endregion


        #region [Codex AI] EnsureAnimeLibToken / RequestAnimeLibToken
        async ValueTask EnsureAnimeLibToken(OnlinesSettings init)
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
                    System.IO.File.WriteAllText(TokenCachePath, JsonConvert.SerializeObject(cache));
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
            var payload = JsonConvert.SerializeObject(new
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

                var result = await Http.Post<JObject>("https://api.cdnlibs.org/api/auth/oauth/token", content, httpversion: 2, timeoutSeconds: 8, headers: headers, useDefaultHeaders: false);
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

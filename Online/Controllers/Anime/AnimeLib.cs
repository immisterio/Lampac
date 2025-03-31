using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using System.Web;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Lampac.Controllers.LITE
{
    public class AnimeLib : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager(AppInit.conf.AnimeLib);

        [HttpGet]
        [Route("lite/animelib")]
        async public Task<ActionResult> Index(string title, string original_title, int year, string uri, string t, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.AnimeLib);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);

            if (string.IsNullOrWhiteSpace(uri))
            {
                #region Поиск
                if (string.IsNullOrWhiteSpace(title) || year == 0)
                    return OnError();

                string memkey = $"animelib:search:{title}:{original_title}";
                if (!hybridCache.TryGetValue(memkey, out List<(string title, string year, string uri, bool coincidence)> catalog))
                {
                    if (rch.IsNotConnected())
                        return ContentTo(rch.connectionMsg);

                    async ValueTask<JObject> goSearch(string q)
                    {
                        if (string.IsNullOrEmpty(q))
                            return null;

                        string req_uri = $"{init.corsHost()}/api/anime?fields[]=rate_avg&fields[]=rate&fields[]=releaseDate&q={HttpUtility.UrlEncode(q)}";
                        var result = rch.enable ? await rch.Get<JObject>(req_uri, httpHeaders(init)) :
                                                  await HttpClient.Get<JObject>(req_uri, httpversion: 2, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));

                        if (result == null || !result.ContainsKey("data"))
                            return null;

                        return result;
                    }

                    var search = await goSearch(original_title) ?? await goSearch(title);
                    if (search == null)
                        return OnError(proxyManager, refresh_proxy: !rch.enable);

                    catalog = new List<(string title, string year, string uri, bool coincidence)>();

                    foreach (var anime in search["data"])
                    {
                        string rus_name = anime.Value<string>("rus_name");
                        string eng_name = anime.Value<string>("eng_name");
                        string slug_url = anime.Value<string>("slug_url");
                        string releaseDate = anime.Value<string>("releaseDate");

                        if (string.IsNullOrEmpty(slug_url))
                            continue;

                        var model = ($"{rus_name} / {eng_name}", (releaseDate != null ? releaseDate.Split("-")[0] : "0"), slug_url, false);

                        if (StringConvert.SearchName(title) == StringConvert.SearchName(rus_name) || StringConvert.SearchName(title) == StringConvert.SearchName(eng_name))
                        {
                            if (!string.IsNullOrEmpty(releaseDate) && releaseDate.StartsWith(year.ToString()))
                                model.Item4 = true;
                        }

                        catalog.Add(model);
                    }

                    if (catalog.Count == 0)
                        return OnError();

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(memkey, catalog, cacheTime(40, init: init));
                }

                if (catalog.Where(i => i.coincidence).Count() == 1)
                    return LocalRedirect(accsArgs($"/lite/animelib?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(catalog.First(i => i.coincidence).uri)}"));

                var stpl = new SimilarTpl(catalog.Count);

                foreach (var res in catalog)
                    stpl.Append(res.title, res.year, string.Empty, $"{host}/lite/animelib?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(res.uri)}");

                return ContentTo(rjson ? stpl.ToJson() : stpl.ToHtml());
                #endregion
            }
            else 
            {
                #region Серии
                string memKey = $"animelib:playlist:{uri}";
                if (!hybridCache.TryGetValue(memKey, out JArray episodes))
                {
                    if (rch.IsNotConnected())
                        return ContentTo(rch.connectionMsg);

                    string req_uri = $"{init.corsHost()}/api/episodes?anime_id={uri}";

                    var root = rch.enable ? await rch.Get<JObject>(req_uri, httpHeaders(init)) : 
                                            await HttpClient.Get<JObject>(req_uri, timeoutSeconds: 8, httpversion: 2, proxy: proxyManager.Get(), headers: httpHeaders(init));

                    if (root == null || !root.ContainsKey("data"))
                        return OnError(proxyManager, refresh_proxy: !rch.enable);

                    episodes = root["data"].ToObject<JArray>();

                    if (episodes.Count == 0)
                        return OnError();

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(memKey, episodes, cacheTime(30, init: init));
                }

                #region Перевод
                memKey = $"animelib:video:{episodes.First.Value<int>("id")}";
                if (!hybridCache.TryGetValue(memKey, out JArray players))
                {
                    if (rch.IsNotConnected())
                        return ContentTo(rch.connectionMsg);

                    string req_uri = $"{init.corsHost()}/api/episodes/{episodes.First.Value<int>("id")}";

                    var root = rch.enable ? await rch.Get<JObject>(req_uri, httpHeaders(init)) : 
                                            await HttpClient.Get<JObject>(req_uri, httpversion: 2, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));

                    if (root == null || !root.ContainsKey("data"))
                        return OnError(proxyManager, refresh_proxy: !rch.enable);

                    players = root["data"]["players"].ToObject<JArray>();
                    hybridCache.Set(memKey, players, cacheTime(30, init: init));
                }

                var vtpl = new VoiceTpl();
                string activTranslate = t;

                foreach (var player in players)
                {
                    if (player.Value<string>("player") != "Animelib")
                        continue;

                    string name = player["team"].Value<string>("name");

                    if (string.IsNullOrEmpty(activTranslate))
                        activTranslate = name;

                    vtpl.Append(name, activTranslate == name, $"{host}/lite/animelib?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(uri)}&t={HttpUtility.UrlEncode(name)}");
                }
                #endregion

                var etpl = new EpisodeTpl();

                foreach (var episode in episodes)
                {
                    int id = episode.Value<int>("id");
                    string number = episode.Value<string>("number");
                    string season = episode.Value<string>("season");

                    string name = episode.Value<string>("name");
                    name = string.IsNullOrEmpty(name) ? title : $"{title} / {name}";

                    string link = $"{host}/lite/animelib/video?id={id}&voice={HttpUtility.UrlEncode(activTranslate)}&title={HttpUtility.UrlEncode(title)}";

                    etpl.Append($"{number} серия", name, season, number, link, "call", streamlink: accsArgs($"{link}&play=true"));
                }
                
                if (rjson)
                    return ContentTo(etpl.ToJson(vtpl));

                return ContentTo(vtpl.ToHtml() + etpl.ToHtml());
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/animelib/video")]
        async public Task<ActionResult> Video(string title, long id, string voice, bool play)
        {
            var init = await loadKit(AppInit.conf.AnimeLib);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var headers = httpHeaders(init);

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotConnected() && init.rhub_fallback && play)
                rch.Disabled();

            var cache = await InvokeCache<JArray>($"animelib:video:{id}", cacheTime(30, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string req_uri = $"{init.corsHost()}/api/episodes/{id}";

                var root = rch.enable ? await rch.Get<JObject>(req_uri, headers) :
                                        await HttpClient.Get<JObject>(req_uri, httpversion: 2, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: headers);

                if (root == null || !root.ContainsKey("data"))
                    return res.Fail("data");

                return root["data"]["players"].ToObject<JArray>();
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg, gbcache: !rch.enable);

            List<(string link, string quality)> goStreams(string _voice)
            {
                var _streams = new List<(string link, string quality)>() { Capacity = 5 };

                foreach (var player in cache.Value)
                {
                    if (player.Value<string>("player") != "Animelib")
                        continue;

                    if (!string.IsNullOrEmpty(_voice) && _voice != player["team"].Value<string>("name"))
                        continue;

                    foreach (var item in player["video"]["quality"])
                    {
                        string href = item.Value<string>("href");
                        if (string.IsNullOrEmpty(href))
                            continue;

                        string file = HostStreamProxy(init, "https://video1.anilib.me/.%D0%B0s/" + href, proxy: proxyManager.Get(), headers: headers);

                        _streams.Add((file, $"{item.Value<int>("quality")}p"));
                    }

                    break;
                }

                return _streams;
            }

            var streams = goStreams(voice);
            if (streams.Count == 0)
                streams = goStreams(null);

            if (play)
                return Redirect(streams[0].link);

            return ContentTo(VideoTpl.ToJson("play", streams[0].link, title, streamquality: new StreamQualityTpl(streams), vast: init.vast));
        }
        #endregion
    }
}

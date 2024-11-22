using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using System.Web;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using Newtonsoft.Json.Linq;
using Shared.Model.Online;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Controllers.LITE
{
    public class AnimeLib : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("animelib", AppInit.conf.AnimeLib);

        List<HeadersModel> baseHeaders = HeadersModel.Init(
            ("cache-control", "no-cache"),
            ("dnt", "1"),
            ("pragma", "no-cache"),
            ("priority", "u=0, i"),
            ("sec-ch-ua", "\"Chromium\";v=\"130\", \"Google Chrome\";v=\"130\", \"Not ? A_Brand\";v=\"99\""),
            ("sec-ch-ua-mobile", "?0"),
            ("sec-ch-ua-platform", "\"Windows\""),
            ("sec-fetch-dest", "document"),
            ("sec-fetch-mode", "navigate"),
            ("sec-fetch-site", "none"),
            ("sec-fetch-user", "?1"),
            ("upgrade-insecure-requests", "1")
        );

        [HttpGet]
        [Route("lite/animelib")]
        async public Task<ActionResult> Index(string account_email, string title, string original_title, int year, string uri, string t, bool rjson = false)
        {
            var init = AppInit.conf.AnimeLib;

            if (!init.enable)
                return OnError();

            if (init.rhub && !AppInit.conf.rch.enable)
                return ShowError(RchClient.ErrorMsg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            var rch = new RchClient(HttpContext, host, init.rhub);
            var rheader = httpHeaders(init, baseHeaders).ToDictionary(k => k.name, v => v.val);

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
                        var result = init.rhub ? await rch.Get<JObject>(req_uri, rheader) :
                                                 await HttpClient.Get<JObject>(req_uri, httpversion: 2, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init, baseHeaders));

                        if (result == null || !result.ContainsKey("data"))
                            return null;

                        return result;
                    }

                    var search = await goSearch(original_title) ?? await goSearch(title);
                    if (search == null)
                        return OnError(proxyManager, refresh_proxy: !init.rhub);

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

                    if (!init.rhub)
                        proxyManager.Success();

                    hybridCache.Set(memkey, catalog, cacheTime(40, init: init));
                }

                if (catalog.Where(i => i.coincidence).Count() == 1)
                    return LocalRedirect($"/lite/animelib?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(catalog.First(i => i.coincidence).uri)}&account_email={HttpUtility.UrlEncode(account_email)}");

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
                if (!memoryCache.TryGetValue(memKey, out JArray episodes))
                {
                    if (rch.IsNotConnected())
                        return ContentTo(rch.connectionMsg);

                    string req_uri = $"{init.corsHost()}/api/episodes?anime_id={uri}";

                    var root = init.rhub ? await rch.Get<JObject>(req_uri, rheader) : 
                                           await HttpClient.Get<JObject>(req_uri, timeoutSeconds: 8, httpversion: 2, proxy: proxyManager.Get(), headers: httpHeaders(init, baseHeaders));

                    if (root == null || !root.ContainsKey("data"))
                        return OnError(proxyManager, refresh_proxy: !init.rhub);

                    episodes = root["data"].ToObject<JArray>();

                    if (episodes.Count == 0)
                        return OnError();

                    if (!init.rhub)
                        proxyManager.Success();

                    memoryCache.Set(memKey, episodes, cacheTime(30, init: init));
                }

                #region Перевод
                memKey = $"animelib:video:{episodes.First.Value<int>("id")}";
                if (!memoryCache.TryGetValue(memKey, out JArray players))
                {
                    if (rch.IsNotConnected())
                        return ContentTo(rch.connectionMsg);

                    string req_uri = $"{init.corsHost()}/api/episodes/{episodes.First.Value<int>("id")}";

                    var root = init.rhub ? await rch.Get<JObject>(req_uri, rheader) : 
                                           await HttpClient.Get<JObject>(req_uri, httpversion: 2, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init, baseHeaders));

                    if (root == null || !root.ContainsKey("data"))
                        return OnError(proxyManager, refresh_proxy: !init.rhub);

                    players = root["data"]["players"].ToObject<JArray>();
                    memoryCache.Set(memKey, players, cacheTime(30, init: init));
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

                    etpl.Append($"{number} серия", name, season, number, link, "call", streamlink: $"{link}&account_email={HttpUtility.UrlEncode(account_email)}&play=true");
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
            var init = AppInit.conf.AnimeLib;

            if (!init.enable)
                return OnError();

            var rch = new RchClient(HttpContext, host, init.rhub);

            string memKey = $"animelib:video:{id}";
            if (!memoryCache.TryGetValue(memKey, out JArray players))
            {
                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                string req_uri = $"{init.corsHost()}/api/episodes/{id}";

                var root = init.rhub ? await rch.Get<JObject>(req_uri, httpHeaders(init, baseHeaders).ToDictionary(k => k.name, v => v.val)) :
                                       await HttpClient.Get<JObject>(req_uri, httpversion: 2, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init, baseHeaders));

                if (root == null || !root.ContainsKey("data"))
                    return OnError(proxyManager, refresh_proxy: !init.rhub);

                players = root["data"]["players"].ToObject<JArray>();

                if (!init.rhub)
                    proxyManager.Success();

                memoryCache.Set(memKey, players, cacheTime(30, init: init));
            }

            List<(string link, string quality)> goStreams(string _voice)
            {
                var _streams = new List<(string link, string quality)>() { Capacity = 5 };

                foreach (var player in players)
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

                        string file = HostStreamProxy(init, "https://video1.anilib.me/.%D0%B0s/" + href, proxy: proxyManager.Get(), plugin: "animelib");
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

            string streansquality = "\"quality\": {" + string.Join(",", streams.Select(s => $"\"{s.quality}\":\"{s.link}\"")) + "}";
            return Content("{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + title + "\", " + streansquality + "}", "application/json; charset=utf-8");
        }
        #endregion
    }
}

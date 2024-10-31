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
        async public Task<ActionResult> Index(string title, int year, string uri, string t, string account_email, bool rjson = false)
        {
            var init = AppInit.conf.AnimeLib;

            if (!init.enable)
                return OnError();

            if (init.rhub)
                return ShowError(RchClient.ErrorMsg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            if (string.IsNullOrWhiteSpace(uri))
            {
                if (string.IsNullOrWhiteSpace(title) || year == 0)
                    return OnError();

                #region Поиск
                string memkey = $"animelib:search:{title}";
                if (!hybridCache.TryGetValue(memkey, out List<(string title, string year, string uri)> catalog))
                {
                    var search = await HttpClient.Get<JObject>($"{init.corsHost()}/api/anime?fields[]=rate_avg&fields[]=rate&fields[]=releaseDate&q={HttpUtility.UrlEncode(title)}", httpversion: 2, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init, baseHeaders));
                    if (search == null || !search.ContainsKey("data"))
                        return OnError(proxyManager);

                    catalog = new List<(string title, string year, string uri)>();

                    foreach (var anime in search["data"])
                    {
                        string rus_name = anime.Value<string>("rus_name");
                        string eng_name = anime.Value<string>("eng_name");
                        string slug_url = anime.Value<string>("slug_url");
                        string releaseDate = anime.Value<string>("releaseDate");

                        if (string.IsNullOrEmpty(slug_url) || string.IsNullOrEmpty(releaseDate))
                            continue;

                        if (StringConvert.SearchName(title) == StringConvert.SearchName(rus_name) || StringConvert.SearchName(title) == StringConvert.SearchName(eng_name))
                        {
                            if (releaseDate.StartsWith(year.ToString()))
                                catalog.Add(($"{rus_name} / {eng_name}", releaseDate.Split("-")[0], slug_url));
                        }
                    }

                    if (catalog.Count == 0)
                        return OnError();

                    proxyManager.Success();
                    hybridCache.Set(memkey, catalog, cacheTime(40, init: init));
                }

                if (catalog.Count == 1)
                    return LocalRedirect($"/lite/animelib?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(catalog[0].uri)}&account_email={HttpUtility.UrlEncode(account_email)}");

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
                    var root = await HttpClient.Get<JObject>($"{init.corsHost()}/api/episodes?anime_id={uri}", timeoutSeconds: 8, httpversion: 2, proxy: proxyManager.Get(), headers: httpHeaders(init, baseHeaders));
                    if (root == null || !root.ContainsKey("data"))
                        return OnError(proxyManager);

                    episodes = root["data"].ToObject<JArray>();

                    if (episodes.Count == 0)
                        return OnError();

                    proxyManager.Success();
                    memoryCache.Set(memKey, episodes, cacheTime(30, init: init));
                }

                #region Перевод
                memKey = $"animelib:video:{episodes.First.Value<int>("id")}";
                if (!memoryCache.TryGetValue(memKey, out JArray players))
                {
                    var root = await HttpClient.Get<JObject>($"{init.corsHost()}/api/episodes/{episodes.First.Value<int>("id")}", httpversion: 2, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init, baseHeaders));
                    if (root == null || !root.ContainsKey("data"))
                        return OnError(proxyManager);

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

                    etpl.Append($"{number} серия", name, season, number, link, "call", streamlink: $"{link}&play=true");
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

            string memKey = $"animelib:video:{id}";
            if (!memoryCache.TryGetValue(memKey, out JArray players))
            {
                var root = await HttpClient.Get<JObject>($"{init.corsHost()}/api/episodes/{id}", httpversion: 2, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init, baseHeaders));
                if (root == null || !root.ContainsKey("data"))
                    return OnError(proxyManager);

                players = root["data"]["players"].ToObject<JArray>();

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

                        _streams.Add(("https://video1.anilib.me/.%D0%B0s/" + href, $"{item.Value<int>("quality")}p"));
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

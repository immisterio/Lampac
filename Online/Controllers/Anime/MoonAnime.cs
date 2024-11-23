using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using System.Web;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Shared.Model.Online;
using System.Linq;

namespace Lampac.Controllers.LITE
{
    public class MoonAnime : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("moonanime", AppInit.conf.MoonAnime);

        [HttpGet]
        [Route("lite/moonanime")]
        async public Task<ActionResult> Index(string account_email, string imdb_id, string title, string original_title, long animeid, string t, int s = -1, bool rjson = false)
        {
            var init = AppInit.conf.MoonAnime;

            if (!init.enable || string.IsNullOrEmpty(init.token))
                return OnError();

            if (init.rhub)
                return ShowError(RchClient.ErrorMsg);

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            if (animeid == 0)
            {
                #region Поиск
                string memkey = $"moonanime:search:{imdb_id}:{title}:{original_title}";
                if (!hybridCache.TryGetValue(memkey, out List<(string title, string year, long id)> catalog))
                {
                    async ValueTask<JObject> goSearch(string arg)
                    {
                        if (string.IsNullOrEmpty(arg.Split("=")?[1]))
                            return null;

                        var search = await HttpClient.Get<JObject>($"{init.corsHost()}/api/2.0/titles?api_key={init.token}&limit=20" + arg, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                        if (search == null || !search.ContainsKey("anime_list"))
                            return null;

                        if (search["anime_list"].Count() == 0)
                            return null;

                        return search;
                    }

                    JObject search = await goSearch($"&imdbid={imdb_id}") ?? await goSearch($"&japanese_title={HttpUtility.UrlEncode(original_title)}") ?? await goSearch($"&title={HttpUtility.UrlEncode(title)}");
                    if (search == null)
                        return OnError(proxyManager);

                    catalog = new List<(string title, string year, long id)>();

                    foreach (var anime in search["anime_list"])
                    {
                        string _titl = anime.Value<string>("title");
                        int year = anime.Value<int>("year");

                        if (string.IsNullOrEmpty(_titl))
                            continue;

                        catalog.Add((_titl, year.ToString(), anime.Value<long>("id")));
                    }

                    if (catalog.Count == 0)
                        return OnError();

                    proxyManager.Success();
                    hybridCache.Set(memkey, catalog, cacheTime(40, init: init));
                }

                if (catalog.Count == 1)
                    return LocalRedirect($"/lite/moonanime?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&animeid={catalog[0].id}&account_email={HttpUtility.UrlEncode(account_email)}");

                var stpl = new SimilarTpl(catalog.Count);

                foreach (var res in catalog)
                    stpl.Append(res.title, res.year, string.Empty, $"{host}/lite/moonanime?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&animeid={res.id}");

                return ContentTo(rjson ? stpl.ToJson() : stpl.ToHtml());
                #endregion
            }
            else 
            {
                #region Серии
                string memKey = $"moonanime:playlist:{animeid}";
                if (!memoryCache.TryGetValue(memKey, out JArray root))
                {
                    root = await HttpClient.Get<JArray>($"{init.corsHost()}/api/2.0/title/{animeid}/videos?api_key={init.token}", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (root == null)
                        return OnError(proxyManager);

                    proxyManager.Success();
                    memoryCache.Set(memKey, root, cacheTime(30, init: init));
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

                    foreach (var voices in root)
                    {
                        foreach (var voice in voices.ToObject<Dictionary<string, Dictionary<string, JArray>>>())
                        {
                            if (voice.Key != activTranslate)
                                continue;

                            foreach (var season in voice.Value)
                            {
                                if (season.Key != s.ToString())
                                    continue;

                                foreach (var folder in season.Value)
                                {
                                    int episode = folder.Value<int>("episode");
                                    string vod = folder.Value<string>("vod");

                                    string link = $"{host}/lite/moonanime/video?vod={HttpUtility.UrlEncode(vod)}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}";
                                    string streamlink = $"{link.Replace("/video", "/video.m3u8")}&account_email={HttpUtility.UrlEncode(account_email)}&play=true";

                                    etpl.Append($"{episode} серия", title, s.ToString(), episode.ToString(), link, "call", streamlink: streamlink);
                                }
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
        [Route("lite/moonanime/video")]
        [Route("lite/moonanime/video.m3u8")]
        async public Task<ActionResult> Video(string vod, bool play, string title, string original_title)
        {
            var init = AppInit.conf.MoonAnime;
            if (!init.enable || string.IsNullOrEmpty(init.token))
                return OnError();

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            string memKey = $"moonanime:vod:{vod}";
            if (!hybridCache.TryGetValue(memKey, out (string file, string subtitle) cache))
            {
                string iframe = await HttpClient.Get(vod + "?player=partner", timeoutSeconds: 10, httpversion: 2, proxy: proxyManager.Get(), headers: httpHeaders(init, HeadersModel.Init(
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
                ("origin", "https://anitube.in.ua"),
                ("priority", "u=1, i"),
                ("sec-ch-ua", "\"Chromium\";v=\"130\", \"Microsoft Edge\";v=\"130\", \"Not?A_Brand\";v=\"99\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "cross-site"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0")
            ), plugin: "moonanime");


            if (play)
                return Redirect(file);

            return Content("{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + (title ?? original_title) + "\", \"subtitles\":" + subtitles.ToJson() + "}", "application/json; charset=utf-8");
        }
        #endregion
    }
}

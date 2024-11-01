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
using Newtonsoft.Json;
using Shared.Model.Online;

namespace Lampac.Controllers.LITE
{
    public class MoonAnime : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("moonanime", AppInit.conf.MoonAnime);

        [HttpGet]
        [Route("lite/moonanime")]
        async public Task<ActionResult> Index(string imdb_id, string title, string uri, long animeid, string t, bool rjson = false)
        {
            var init = AppInit.conf.MoonAnime;

            if (!init.enable || string.IsNullOrEmpty(init.token))
                return OnError();

            if (init.rhub)
                return ShowError(RchClient.ErrorMsg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            if (string.IsNullOrWhiteSpace(uri))
            {
                #region Поиск
                string memkey = $"moonanime:search:{imdb_id}";
                if (!hybridCache.TryGetValue(memkey, out List<(string title, string year, string uri, long id)> catalog))
                {
                    var search = await HttpClient.Get<JObject>($"{init.corsHost()}/api/2.0/titles?imdbid={imdb_id}&api_key={init.token}&limit=20", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (search == null || !search.ContainsKey("anime_list"))
                        return OnError(proxyManager);

                    catalog = new List<(string title, string year, string uri, long)>();

                    foreach (var anime in search["anime_list"])
                    {
                        string _titl = anime.Value<string>("title");
                        int year = anime.Value<int>("year");
                        string url = anime.Value<string>("url");

                        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(_titl))
                            continue;

                        catalog.Add((_titl, year.ToString(), url, anime.Value<long>("id")));
                    }

                    if (catalog.Count == 0)
                        return OnError();

                    proxyManager.Success();
                    hybridCache.Set(memkey, catalog, cacheTime(40, init: init));
                }

                var stpl = new SimilarTpl(catalog.Count);

                foreach (var res in catalog)
                    stpl.Append(res.title, res.year, string.Empty, $"{host}/lite/moonanime?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(res.uri)}&animeid={res.id}");

                return ContentTo(rjson ? stpl.ToJson() : stpl.ToHtml());
                #endregion
            }
            else 
            {
                #region Серии
                string memKey = $"moonanime:playlist:{uri}";
                if (!memoryCache.TryGetValue(memKey, out JArray voices))
                {
                    string iframe = await HttpClient.Get(uri + "?player=partner", timeoutSeconds: 10, httpversion: 2, proxy: proxyManager.Get(), headers: httpHeaders(init, HeadersModel.Init(
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

                    string videoList = Regex.Match(iframe, "file: ?'([^\n\r]+)',").Groups[1].Value;
                    if (string.IsNullOrEmpty(videoList))
                        return OnError(proxyManager);

                    voices = JsonConvert.DeserializeObject<JArray>(videoList);
                    if (voices == null || voices.Count == 0)
                        return OnError();

                    proxyManager.Success();
                    memoryCache.Set(memKey, voices, cacheTime(30, init: init));
                }

                #region информация о сезоне
                memKey = $"moonanime:season:{animeid}";
                if (!memoryCache.TryGetValue(memKey, out int season))
                {
                    var info = await HttpClient.Get<JObject>($"{init.corsHost()}/api/2.0/title/{animeid}?api_key={init.token}", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (info != null)
                    {
                        season = info.Value<int>("season");
                        memoryCache.Set(memKey, season, cacheTime(180, init: init));
                    }
                }
                #endregion

                #region Перевод
                var vtpl = new VoiceTpl();
                string activTranslate = t;

                foreach (var voice in voices)
                {
                    string name = voice.Value<string>("title");

                    if (string.IsNullOrEmpty(activTranslate))
                        activTranslate = name;

                    vtpl.Append(name, activTranslate == name, $"{host}/lite/moonanime?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(uri)}&animeid={animeid}&t={HttpUtility.UrlEncode(name)}");
                }
                #endregion

                var etpl = new EpisodeTpl();

                foreach (var voice in voices)
                {
                    if (voice.Value<string>("title") != activTranslate)
                        continue;

                    foreach (var folder in voice["folder"])
                    {
                        string name = folder.Value<string>("title");
                        string subtitle = folder.Value<string>("subtitle");
                        string thumbnails = folder.Value<string>("thumbnails");

                        var subtitles = new SubtitleTpl();

                        if (subtitle != null && (subtitle.Contains(".str") || subtitle.Contains(".vtt")))
                            subtitles.Append("ukr", subtitle);

                        if (thumbnails != null && (thumbnails.Contains(".str") || thumbnails.Contains(".vtt")))
                            subtitles.Append("ukr", thumbnails);

                        string number = Regex.Match(name, "Серія ([0-9]+)").Groups[1].Value;
                        string file = HostStreamProxy(init, folder.Value<string>("file"), proxy: proxyManager.Get(), headers: HeadersModel.Init(
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

                        string _titl = Regex.Replace(name, "Серія ([0-9]+)", "").Trim();

                        etpl.Append($"{number} серия", (string.IsNullOrEmpty(_titl) ? title : $"{title} / {_titl}"), season.ToString(), number, file, subtitles: subtitles);
                    }
                }

                if (rjson)
                    return ContentTo(etpl.ToJson(vtpl));

                return ContentTo(vtpl.ToHtml() + etpl.ToHtml());
                #endregion
            }
        }
    }
}

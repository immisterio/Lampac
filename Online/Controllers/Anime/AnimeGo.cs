using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Online.Controllers
{
    public class AnimeGo : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager(AppInit.conf.AnimeGo);

        [HttpGet]
        [Route("lite/animego")]
        async public ValueTask<ActionResult> Index(string title, int year, int pid, int s, string t, bool rjson = false, bool similar = false)
        {
            var init = await loadKit(AppInit.conf.AnimeGo);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            var headers_stream = httpHeaders(init.host, init.headers_stream);

            if (pid == 0)
            {
                #region Поиск
                string memkey = $"animego:search:{title}";

                return await InvkSemaphore(init, memkey, async () =>
                {
                    if (!hybridCache.TryGetValue(memkey, out List<(string title, string year, string pid, string s, string img)> catalog, inmemory: false))
                    {
                        string search = await Http.Get($"{init.corsHost()}/search/anime?q={HttpUtility.UrlEncode(title)}", timeoutSeconds: 10, proxy: proxyManager.Get(), headers: httpHeaders(init), httpversion: 2);
                        if (search == null)
                            return OnError(proxyManager);

                        var rows = search.Split("class=\"p-poster__stack\"");

                        catalog = new List<(string title, string year, string pid, string s, string img)>(rows.Length);

                        foreach (string row in rows.Skip(1))
                        {
                            string player_id = Regex.Match(row, "data-ajax-url=\"/[^\"]+-([0-9]+)\"").Groups[1].Value;
                            string name = Regex.Match(row, "card-title text-truncate\"><a [^>]+>([^<]+)<").Groups[1].Value;
                            string animeyear = Regex.Match(row, "class=\"anime-year\"><a [^>]+>([0-9]{4})<").Groups[1].Value;
                            string img = Regex.Match(row, "data-original=\"([^\"]+)\"").Groups[1].Value;
                            if (string.IsNullOrEmpty(img))
                                img = null;

                            if (!string.IsNullOrWhiteSpace(player_id) && !string.IsNullOrWhiteSpace(name) && StringConvert.SearchName(name).Contains(StringConvert.SearchName(title)))
                            {
                                string season = "0";
                                if (animeyear == year.ToString() && StringConvert.SearchName(name) == StringConvert.SearchName(title))
                                    season = "1";

                                catalog.Add((name, Regex.Match(row, ">([0-9]{4})</a>").Groups[1].Value, player_id, season, img));
                            }
                        }

                        if (catalog.Count == 0)
                            return OnError();

                        proxyManager.Success();
                        hybridCache.Set(memkey, catalog, cacheTime(40, init: init), inmemory: false);
                    }

                    if (!similar && catalog.Count == 1)
                        return LocalRedirect(accsArgs($"/lite/animego?title={HttpUtility.UrlEncode(title)}&pid={catalog[0].pid}&s={catalog[0].s}"));

                    var stpl = new SimilarTpl(catalog.Count);

                    foreach (var res in catalog)
                    {
                        string uri = $"{host}/lite/animego?title={HttpUtility.UrlEncode(title)}&pid={res.pid}&s={res.s}";
                        stpl.Append(res.title, res.year, string.Empty, uri, PosterApi.Size(res.img));
                    }

                    return ContentTo(rjson ? stpl.ToJson() : stpl.ToHtml());
                });
                #endregion
            }
            else 
            {
                #region Серии
                string memKey = $"animego:playlist:{pid}";

                return await InvkSemaphore(init, memKey, async () =>
                {
                    if (!hybridCache.TryGetValue(memKey, out (string translation, List<(string episode, string uri)> links, List<(string name, string id)> translations) cache))
                    {
                        #region content
                        var player = await Http.Get<JObject>($"{init.corsHost()}/anime/{pid}/player?_allow=true", timeoutSeconds: 10, proxy: proxyManager.Get(), httpversion: 2, headers: httpHeaders(init, HeadersModel.Init(
                            ("cache-control", "no-cache"),
                            ("dnt", "1"),
                            ("pragma", "no-cache"),
                            ("referer", $"{init.host}/"),
                            ("sec-fetch-dest", "empty"),
                            ("sec-fetch-mode", "cors"),
                            ("sec-fetch-site", "same-origin"),
                            ("x-requested-with", "XMLHttpRequest")
                        )));

                        string content = player?.Value<string>("content");
                        if (string.IsNullOrWhiteSpace(content))
                            return OnError(proxyManager);
                        #endregion

                        var g = Regex.Match(content, "data-player=\"(https?:)?//(aniboom\\.[^/]+)/embed/([^\"\\?&]+)\\?episode=1\\&amp;translation=([0-9]+)\"").Groups;
                        if (string.IsNullOrWhiteSpace(g[2].Value) || string.IsNullOrWhiteSpace(g[3].Value) || string.IsNullOrWhiteSpace(g[4].Value))
                            return OnError();

                        #region links
                        var match = Regex.Match(content, "data-episode=\"([0-9]+)\"");
                        cache.links = new List<(string episode, string uri)>(match.Length);

                        while (match.Success)
                        {
                            if (!string.IsNullOrWhiteSpace(match.Groups[1].Value))
                                cache.links.Add((match.Groups[1].Value, $"video.m3u8?host={g[2].Value}&token={g[3].Value}&e={match.Groups[1].Value}"));

                            match = match.NextMatch();
                        }

                        if (cache.links.Count == 0)
                            return OnError();
                        #endregion

                        #region translation / translations
                        match = Regex.Match(content, "data-player=\"(https?:)?//aniboom\\.[^/]+/embed/[^\"\\?&]+\\?episode=[0-9]+\\&amp;translation=([0-9]+)\"[\n\r\t ]+data-provider=\"[0-9]+\"[\n\r\t ]+data-provide-dubbing=\"([0-9]+)\"");

                        cache.translation = g[4].Value;
                        cache.translations = new List<(string name, string id)>(match.Length);

                        while (match.Success)
                        {
                            if (!string.IsNullOrWhiteSpace(match.Groups[2].Value) && !string.IsNullOrWhiteSpace(match.Groups[3].Value))
                            {
                                string name = Regex.Match(content, $"data-dubbing=\"{match.Groups[3].Value}\"><span [^>]+>[\n\r\t ]+([^\n\r<]+)").Groups[1].Value.Trim();
                                if (!string.IsNullOrWhiteSpace(name))
                                    cache.translations.Add((name, match.Groups[2].Value));
                            }

                            match = match.NextMatch();
                        }
                        #endregion

                        proxyManager.Success();
                        hybridCache.Set(memKey, cache, cacheTime(30, init: init));
                    }

                    #region Перевод
                    var vtpl = new VoiceTpl(cache.translations.Count);
                    if (string.IsNullOrWhiteSpace(t))
                        t = cache.translation;

                    foreach (var translation in cache.translations)
                    {
                        string link = $"{host}/lite/animego?pid={pid}&title={HttpUtility.UrlEncode(title)}&s={s}&t={translation.id}";
                        vtpl.Append(translation.name, t == translation.id, link);
                    }
                    #endregion

                    var etpl = new EpisodeTpl(cache.links.Count);
                    string sArhc = s.ToString();

                    foreach (var l in cache.links)
                    {
                        string hls = accsArgs($"{host}/lite/animego/{l.uri}&t={t ?? cache.translation}");

                        etpl.Append($"{l.episode} серия", title, sArhc, l.episode, hls, "play", headers: headers_stream);
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
        [Route("lite/animego/video.m3u8")]
        async public ValueTask<ActionResult> Video(string host, string token, string t, int e)
        {
            var init = await loadKit(AppInit.conf.AnimeGo);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            string memKey = $"animego:video:{token}:{t}:{e}";

            return await InvkSemaphore(init, memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out string hls))
                {
                    string embed = await Http.Get($"https://{host}/embed/{token}?episode={e}&translation={t}", timeoutSeconds: 10, proxy: proxyManager.Get(), httpversion: 2, headers: httpHeaders(init, HeadersModel.Init(
                        ("cache-control", "no-cache"),
                        ("dnt", "1"),
                        ("pragma", "no-cache"),
                        ("referer", $"{init.host}/"),
                        ("sec-fetch-dest", "empty"),
                        ("sec-fetch-mode", "cors"),
                        ("sec-fetch-site", "same-origin"),
                        ("x-requested-with", "XMLHttpRequest")
                    )));

                    if (string.IsNullOrWhiteSpace(embed))
                        return OnError(proxyManager);

                    embed = embed.Replace("&quot;", "\"").Replace("\\", "");

                    hls = Regex.Match(embed, "\"hls\":\"\\{\"src\":\"(https?:)?(//[^\"]+\\.m3u8)\"").Groups[2].Value;
                    if (string.IsNullOrWhiteSpace(hls))
                        return OnError(proxyManager);

                    hls = "https:" + hls;

                    proxyManager.Success();
                    hybridCache.Set(memKey, hls, cacheTime(30, init: init));
                }

                return Redirect(HostStreamProxy(init, hls, proxy: proxyManager.Get()));
            });
        }
        #endregion
    }
}

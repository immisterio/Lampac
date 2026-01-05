using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Engine.RxEnumerate;

namespace Online.Controllers
{
    public class AnimeGo : BaseOnlineController
    {
        public AnimeGo() : base(AppInit.conf.AnimeGo) { }

        [HttpGet]
        [Route("lite/animego")]
        async public Task<ActionResult> Index(string title, int year, int pid, int s, string t, bool similar = false)
        {
            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            if (await IsRequestBlocked(rch: false))
                return badInitMsg;

            var headers_stream = httpHeaders(init.host, init.headers_stream);

            if (pid == 0)
            {
                #region Поиск
                return await InvkSemaphore($"animego:search:{title}", async key =>
                {
                    if (!hybridCache.TryGetValue(key, out List<(string title, string year, string pid, string s, string img)> catalog, inmemory: false))
                    {
                        string search = await httpHydra.Get($"{init.corsHost()}/search/anime?q={HttpUtility.UrlEncode(title)}");
                        if (search == null)
                            return OnError(refresh_proxy: true);

                        var rx = Rx.Split("class=\"p-poster__stack\"", search, 1);

                        catalog = new List<(string title, string year, string pid, string s, string img)>(rx.Count);

                        foreach (var row in rx.Rows())
                        {
                            string player_id = row.Match("data-ajax-url=\"/[^\"]+-([0-9]+)\"");
                            string name = row.Match("card-title text-truncate\"><a [^>]+>([^<]+)<");
                            string animeyear = row.Match("class=\"anime-year\"><a [^>]+>([0-9]{4})<");
                            string img = row.Match("data-original=\"([^\"]+)\"");
                            if (string.IsNullOrEmpty(img))
                                img = null;

                            if (!string.IsNullOrWhiteSpace(player_id) && !string.IsNullOrWhiteSpace(name) && StringConvert.SearchName(name).Contains(StringConvert.SearchName(title)))
                            {
                                string season = "0";
                                if (animeyear == year.ToString() && StringConvert.SearchName(name) == StringConvert.SearchName(title))
                                    season = "1";

                                catalog.Add((name, row.Match(">([0-9]{4})</a>"), player_id, season, img));
                            }
                        }

                        if (catalog.Count == 0)
                            return OnError();

                        proxyManager?.Success();
                        hybridCache.Set(key, catalog, cacheTime(40), inmemory: false);
                    }

                    if (!similar && catalog.Count == 1)
                        return LocalRedirect(accsArgs($"/lite/animego?title={HttpUtility.UrlEncode(title)}&pid={catalog[0].pid}&s={catalog[0].s}"));

                    var stpl = new SimilarTpl(catalog.Count);

                    foreach (var res in catalog)
                    {
                        string uri = $"{host}/lite/animego?title={HttpUtility.UrlEncode(title)}&pid={res.pid}&s={res.s}";
                        stpl.Append(res.title, res.year, string.Empty, uri, PosterApi.Size(res.img));
                    }

                    return await ContentTpl(stpl);
                });
                #endregion
            }
            else 
            {
                #region Серии
                return await InvkSemaphore($"animego:playlist:{pid}", async key =>
                {
                    if (!hybridCache.TryGetValue(key, out (string translation, List<(string episode, string uri)> links, List<(string name, string id)> translations) cache))
                    {
                        #region content
                        var player = await httpHydra.Get<JObject>($"{init.corsHost()}/anime/{pid}/player?_allow=true", addheaders: HeadersModel.Init(
                            ("cache-control", "no-cache"),
                            ("dnt", "1"),
                            ("pragma", "no-cache"),
                            ("referer", $"{init.host}/"),
                            ("sec-fetch-dest", "empty"),
                            ("sec-fetch-mode", "cors"),
                            ("sec-fetch-site", "same-origin"),
                            ("x-requested-with", "XMLHttpRequest")
                        ));

                        string content = player?.Value<string>("content");
                        if (string.IsNullOrWhiteSpace(content))
                            return OnError(refresh_proxy: true);
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

                        proxyManager?.Success();
                        hybridCache.Set(key, cache, cacheTime(30));
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

                    var etpl = new EpisodeTpl(vtpl, cache.links.Count);

                    foreach (var l in cache.links)
                    {
                        string hls = accsArgs($"{host}/lite/animego/{l.uri}&t={t ?? cache.translation}");

                        etpl.Append($"{l.episode} серия", title, s.ToString(), l.episode, hls, "play", headers: headers_stream);
                    }

                    return await ContentTpl(etpl);
                });
                #endregion
            }
        }

        #region Video
        [HttpGet]
        [Route("lite/animego/video.m3u8")]
        async public ValueTask<ActionResult> Video(string host, string token, string t, int e)
        {
            if (await IsRequestBlocked(rch: false, rch_check: false))
                return badInitMsg;

            return await InvkSemaphore($"animego:video:{token}:{t}:{e}", async key =>
            {
                if (!hybridCache.TryGetValue(key, out string hls))
                {
                    string embed = await httpHydra.Get($"https://{host}/embed/{token}?episode={e}&translation={t}", addheaders: HeadersModel.Init(
                        ("cache-control", "no-cache"),
                        ("dnt", "1"),
                        ("pragma", "no-cache"),
                        ("referer", $"{init.host}/"),
                        ("sec-fetch-dest", "empty"),
                        ("sec-fetch-mode", "cors"),
                        ("sec-fetch-site", "same-origin"),
                        ("x-requested-with", "XMLHttpRequest")
                    ));

                    if (string.IsNullOrWhiteSpace(embed))
                        return OnError(refresh_proxy: true);

                    embed = embed.Replace("&quot;", "\"").Replace("\\", "");

                    hls = Regex.Match(embed, "\"hls\":\"\\{\"src\":\"(https?:)?(//[^\"]+\\.m3u8)\"").Groups[2].Value;
                    if (string.IsNullOrWhiteSpace(hls))
                        return OnError(refresh_proxy: true);

                    hls = "https:" + hls;

                    proxyManager?.Success();
                    hybridCache.Set(key, hls, cacheTime(30));
                }

                return Redirect(HostStreamProxy(hls));
            });
        }
        #endregion
    }
}

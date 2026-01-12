using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Online.Controllers
{
    public class AniLiberty : BaseOnlineController
    {
        public AniLiberty() : base(AppInit.conf.AniLiberty) { }

        [HttpGet]
        [Route("lite/aniliberty")]
        async public Task<ActionResult> Index(string title, int year, string releases, bool rjson = false, bool similar = false, string source = null, string id = null)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(releases) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.Contains("aniLiberty", StringComparison.OrdinalIgnoreCase)) 
                    releases = id;
            }

            if (string.IsNullOrEmpty(releases))
            {
                #region Поиск
                string stitle = StringConvert.SearchName(title);
                if (string.IsNullOrEmpty(stitle))
                    return OnError();

                rhubFallback:
                var cache = await InvokeCacheResult<List<(string title, string year, int releases, string cover)>>($"aniliberty:search:{title}:{similar}", 40, async e =>
                {
                    var search = await httpHydra.Get<JArray>($"{init.corsHost()}/api/v1/app/search/releases?query={HttpUtility.UrlEncode(title)}");

                    if (search == null || search.Count == 0)
                        return e.Fail("search");

                    bool checkName = true;
                    var catalog = new List<(string title, string year, int releases, string cover)>(search.Count);

                    retry: foreach (var anime in search)
                    {
                        var name = anime["name"];
                        string name_main = StringConvert.SearchName(name.Value<string>("main"));
                        string name_english = StringConvert.SearchName(name.Value<string>("english"));

                        if (!checkName || similar || (name_main != null && name_main.StartsWith(stitle)) || (name_english != null && name_english.StartsWith(stitle)))
                        {
                            int id = anime.Value<int>("id");
                            int releaseDate = anime.Value<int>("year");

                            string img = null;
                            var cover = anime["poster"];
                            if (cover != null)
                                img = init.host + cover.Value<string>("src");

                            catalog.Add(($"{name.Value<string>("main")} / {name.Value<string>("english")}", releaseDate.ToString(), id, img));
                        }
                    }

                    if (catalog.Count == 0)
                    {
                        if (checkName && similar == false)
                        {
                            checkName = false;
                            goto retry;
                        }

                        return e.Fail("catalog");
                    }

                    return e.Success(catalog);
                });

                if (IsRhubFallback(cache))
                    goto rhubFallback;

                if (!similar && cache.Value != null && cache.Value.Count == 1)
                    return LocalRedirect(accsArgs($"/lite/aniliberty?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&releases={cache.Value.First().releases}"));

                return await ContentTpl(cache, () =>
                {
                    var stpl = new SimilarTpl(cache.Value.Count);

                    foreach (var res in cache.Value)
                        stpl.Append(res.title, res.year, string.Empty, $"{host}/lite/aniliberty?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&releases={res.releases}", PosterApi.Size(res.cover));

                    return stpl;

                });
                #endregion
            }
            else 
            {
                #region Серии
                rhubFallback: 
                var cache = await InvokeCacheResult<JObject>($"aniliberty:releases:{releases}", 20, async e =>
                {
                    var root = await httpHydra.Get<JObject>($"{init.corsHost()}/api/v1/anime/releases/{releases}");

                    if (root == null || !root.ContainsKey("episodes"))
                        return e.Fail("episodes");

                    return e.Success(root);
                });

                if (IsRhubFallback(cache))
                    goto rhubFallback;

                return await ContentTpl(cache, () =>
                {
                    var episodes = cache.Value["episodes"] as JArray;
                    var etpl = new EpisodeTpl(episodes.Count);

                    foreach (var episode in episodes)
                    {
                        string alias = cache.Value.Value<string>("alias") ?? "";
                        string season = Regex.Match(alias, "-([0-9]+)(nd|th)").Groups[1].Value;
                        if (string.IsNullOrEmpty(season))
                        {
                            season = Regex.Match(alias, "season-([0-9]+)").Groups[1].Value;
                            if (string.IsNullOrEmpty(season))
                                season = "1";
                        }

                        string number = episode.Value<string>("ordinal");

                        string name = episode.Value<string>("name");
                        name = string.IsNullOrEmpty(name) ? $"{number} серия" : name;

                        var streams = new StreamQualityTpl();
                        foreach (var f in new List<(string quality, string url)>
                        {
                            ("1080p", episode.Value<string>("hls_1080")),
                            ("720p", episode.Value<string>("hls_720")),
                            ("480p", episode.Value<string>("hls_480"))
                        })
                        {
                            if (string.IsNullOrEmpty(f.url))
                                continue;

                            streams.Append(HostStreamProxy(f.url), f.quality);
                        }

                        etpl.Append(name, title, season, number, streams.Firts().link, streamquality: streams);
                    }

                    return etpl;
                });
                #endregion
            }
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.VeoVeo;

namespace Online.Controllers
{
    public class VeoVeo : BaseOnlineController
    {
        #region database
        static List<Movie> databaseCache;

        static IEnumerable<Movie> database
        {
            get
            {
                if (AppInit.conf.multiaccess || databaseCache != null)
                    return databaseCache ??= JsonHelper.ListReader<Movie>("data/veoveo.json", 45000);

                return JsonHelper.IEnumerableReader<Movie>("data/veoveo.json");
            }
        }
        #endregion

        public VeoVeo() : base(AppInit.conf.VeoVeo) { }

        [HttpGet]
        [Route("lite/veoveo")]
        async public Task<ActionResult> Index(long movieid, string imdb_id, long kinopoisk_id, string title, string original_title, int clarification, int s = -1, bool rjson = false, bool similar = false)
        {
            if (await IsRequestBlocked(rch: true, rch_check: !similar))
                return badInitMsg;

            if (movieid == 0)
            {
                if (similar)
                    return Spider(title);

                var movie = search(imdb_id, kinopoisk_id, title, original_title);
                if (movie == null)
                    return Spider(clarification == 1 ? title : (original_title ?? title));

                movieid = movie.Value.id;
            }

            #region media
            var cache = await InvokeCacheResult<JArray>($"{init.plugin}:view:{movieid}", 20, async e =>
            {
                var root = await httpHydra.Get<JArray>($"{init.corsHost()}/balancer-api/proxy/playlists/catalog-api/episodes?content-id={movieid}");

                if (root == null || root.Count == 0)
                    return e.Fail("data");

                return e.Success(root);
            });
            #endregion

            return await ContentTpl(cache, () =>
            {
                if (cache.Value.First["season"].Value<int>("order") == 0)
                {
                    #region Фильм
                    var mtpl = new MovieTpl(title, original_title, 1);

                    string file = cache.Value.First["episodeVariants"]
                        .OrderByDescending(i => i.Value<string>("filepath").Contains(".m3u8"))
                        .First()
                        .Value<string>("filepath");

                    mtpl.Append("1080p", HostStreamProxy(file), vast: init.vast);

                    return mtpl;
                    #endregion
                }
                else
                {
                    #region Сериал
                    if (s == -1)
                    {
                        var tpl = new SeasonTpl();
                        var hash = new HashSet<int>();

                        foreach (var item in cache.Value)
                        {
                            var season = item["season"].Value<int>("order");
                            if (hash.Contains(season))
                                continue;

                            hash.Add(season);
                            string link = $"{host}/lite/veoveo?rjson={rjson}&kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={season}";
                            tpl.Append($"{season} сезон", link, season);
                        }

                        return tpl;
                    }
                    else
                    {
                        var episodes = cache.Value.Where(i => i["season"].Value<int>("order") == s);

                        var etpl = new EpisodeTpl(episodes.Count());
                        string sArhc = s.ToString();

                        foreach (var episode in episodes.OrderBy(i => i.Value<int>("order")))
                        {
                            string name = episode.Value<string>("title");

                            string file = episode["episodeVariants"]
                                .OrderByDescending(i => i.Value<string>("filepath").Contains(".m3u8"))
                                .First()
                                .Value<string>("filepath");

                            if (string.IsNullOrEmpty(file))
                                continue;

                            string stream = HostStreamProxy(file);
                            etpl.Append(name ?? $"{episode.Value<int>("order")} серия", title ?? original_title, sArhc, episode.Value<int>("order").ToString(), stream, vast: init.vast);
                        }

                        return etpl;
                    }
                    #endregion
                }
            });
        }

        #region Spider
        [HttpGet]
        [Route("lite/veoveo-spider")]
        public ActionResult Spider(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            var stpl = new SimilarTpl(100);
            string _t = StringConvert.SearchName(title);
            if (string.IsNullOrEmpty(_t))
                return OnError();

            foreach (var m in database)
            {
                if (stpl.data.Count >= 100)
                    break;

                if (StringConvert.SearchName(m.title, string.Empty).Contains(_t) || StringConvert.SearchName(m.originalTitle, string.Empty).Contains(_t))
                {
                    string uri = $"{host}/lite/veoveo?movieid={m.id}";
                    stpl.Append(m.title ?? m.originalTitle, m.year.ToString(), string.Empty, uri, PosterApi.Find(m.kinopoiskId, m.imdbId));
                }
            }

            return ContentTo(stpl.ToJson());
        }
        #endregion

        #region search
        Movie? search(string imdb_id, long kinopoisk_id, string title, string original_title)
        {
            string stitle = StringConvert.SearchName(title);
            string sorigtitle = StringConvert.SearchName(original_title);

            Movie? goSearch(bool searchToId)
            {
                if (searchToId && kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id))
                    return null;

                foreach (var item in database)
                {
                    if (searchToId)
                    {
                        if (kinopoisk_id > 0)
                        {
                            if (item.kinopoiskId == kinopoisk_id)
                                return item;
                        }

                        if (!string.IsNullOrEmpty(imdb_id))
                        {
                            if (item.imdbId == imdb_id)
                                return item;
                        }
                    }
                    else
                    {
                        if (sorigtitle != null)
                        {
                            if (StringConvert.SearchName(item.originalTitle) == sorigtitle)
                                return item;
                        }

                        if (stitle != null)
                        {
                            if (StringConvert.SearchName(item.title) == stitle)
                                return item;
                        }
                    }
                }

                return null;
            }

            return goSearch(true) ?? goSearch(false);
        }
        #endregion
    }
}

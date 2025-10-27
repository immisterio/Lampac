using Microsoft.AspNetCore.Mvc;
using Shared.PlaywrightCore;

namespace Catalog.Controllers
{
    public class CardController : BaseController
    {
        [HttpGet]
        [Route("catalog/card")]
        public async Task<ActionResult> Index(string plugin, string uri, string type)
        {
            var init = ModInit.goInit(plugin)?.Clone();
            if (init == null || !init.enable)
                return BadRequest("init not found");

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotConnected())
                rch.Disabled();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string memKey = $"catalog:card:{plugin}:{uri}";

            return await InvkSemaphore(init, memKey, async () =>
            {
                #region html
                if (!hybridCache.TryGetValue(memKey, out string html, inmemory: false))
                {
                    string url = $"{init.host}/{uri}";

                    reset:
                    html =
                        rch.enable ? await rch.Get(url, httpHeaders(init))
                        : init.priorityBrowser == "playwright" ? await PlaywrightBrowser.Get(init, url, httpHeaders(init), proxy.data, cookies: init.cookies)
                        : await Http.Get(url, headers: httpHeaders(init), proxy: proxy.proxy, timeoutSeconds: init.timeout);

                    if (html == null)
                    {
                        if (ModInit.IsRhubFallback(init))
                            goto reset;

                        if (!rch.enable)
                            proxyManager.Refresh();

                        return BadRequest("html");
                    }

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(memKey, html, cacheTime(init.cache_time, init: init), inmemory: false);
                }
                #endregion

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var node = doc.DocumentNode;
                var parse = init.card_parse;

                string name = ModInit.nodeValue(node, parse.name, host)?.ToString();
                string original_name = ModInit.nodeValue(node, parse.original_name, host)?.ToString();
                string year = ModInit.nodeValue(node, parse.year, host)?.ToString();

                #region img
                string img = ModInit.nodeValue(node, parse.image, host)?.ToString();

                if (img != null)
                {
                    img = img.Replace("&amp;", "&").Replace("\\", "");

                    if (img.StartsWith("../"))
                        img = $"{init.host}/{img.Replace("../", "")}";
                    else if (img.StartsWith("//"))
                        img = $"https:{img}";
                    else if (img.StartsWith("/"))
                        img = init.host + img;
                    else if (!img.StartsWith("http"))
                        img = $"{init.host}/{img}";
                }
                #endregion

                var jo = new JObject()
                {
                    ["id"] = CrypTo.md5($"{plugin}:{uri}"),
                    ["url"] = $"{host}/catalog/card?plugin={plugin}&uri={HttpUtility.UrlEncode(uri)}&type={type}",
                    ["source_id"] = uri,
                    ["img"] = PosterApi.Size(host, img),

                    ["vote_average"] = 0,
                    ["genres"] = new JArray(),
                    ["production_countries"] = new JArray(),
                    ["production_companies"] = new JArray()
                };

                string overview = ModInit.nodeValue(node, parse.description, host)?.ToString();
                if (!string.IsNullOrEmpty(overview))
                    jo["overview"] = overview;

                if (type == "tv")
                {
                    jo["first_air_date"] = year;
                    jo["name"] = name;

                    if (!string.IsNullOrEmpty(original_name))
                        jo["original_name"] = original_name;
                }
                else
                {
                    jo["release_date"] = year;
                    jo["title"] = name;

                    if (!string.IsNullOrEmpty(original_name))
                        jo["original_title"] = original_name;
                }

                if (init.card_args != null)
                {
                    foreach (var arg in init.card_args)
                    {
                        object val = ModInit.nodeValue(node, arg, host);
                        if (val != null)
                        {
                            if (arg.name_arg is "kp_rating" or "imdb_rating")
                            {
                                string rating = val?.ToString();
                                if (!string.IsNullOrEmpty(rating))
                                {
                                    rating = rating.Length > 3 ? rating.Substring(0, 3) : rating;
                                    if (rating.Length == 1)
                                        rating = $"{rating}.0";

                                    jo[arg.name_arg] = JToken.FromObject(rating);
                                }
                            }
                            else if (val is string && (arg.name_arg is "genres" or "created_by" or "production_countries" or "production_companies" or "networks" or "spoken_languages"))
                            {
                                string arrayStr = val?.ToString();
                                var array = new JArray();

                                if (!string.IsNullOrEmpty(arrayStr))
                                {
                                    foreach (string str in arrayStr.Split(","))
                                    {
                                        if (string.IsNullOrWhiteSpace(str))
                                            continue;

                                        array.Add(new JObject() { ["name"] = str.Trim() });
                                    }

                                    jo[arg.name_arg] = array;
                                }
                            }
                            else if (val is string && (arg.name_arg is "origin_country" or "languages"))
                            {
                                string arrayStr = val?.ToString();
                                var array = new JArray();

                                if (!string.IsNullOrEmpty(arrayStr))
                                {
                                    foreach (string str in arrayStr.Split(","))
                                    {
                                        if (!string.IsNullOrWhiteSpace(str))
                                            array.Add(str.Trim());
                                    }

                                    if (array.Count > 0)
                                        jo[arg.name_arg] = array;
                                }
                            }
                            else
                            {
                                jo[arg.name_arg] = JToken.FromObject(val);
                            }
                        }
                    }
                }

                if (init.tmdb_injects != null && init.tmdb_injects.Length > 0)
                    await Injects(year, jo, init.tmdb_injects);

                if (!jo.ContainsKey("tagline") && !string.IsNullOrEmpty(original_name))
                    jo["tagline"] = original_name;

                return ContentTo(JsonConvert.SerializeObject(jo));
            });
        }


        #region TMDB Injects
        static readonly string[] defaultInjectskeys =
        [
            "imdb_id",
            "external_ids",
            "backdrop_path",
            "created_by",
            "genres",
            "production_companies",
            "production_countries",
            "content_ratings",
            "episode_run_time",
            "languages",
            "number_of_episodes",
            "number_of_seasons",
            "last_episode_to_air",
            "origin_country",
            "original_language",
            "status",
            "networks",
            "seasons",
            "type",
            "budget",
            "spoken_languages",
            "alternative_titles",
            "keywords",

            // &append_to_response=
            "videos",
            "credits",
            "recommendations",
            "similar",
        ];

        static readonly string[] addEmptykeys =
        [
            "tagline",
            "overview",
            "first_air_date",
            "last_air_date",
            "release_date",
            "runtime"
        ];

        async Task Injects(string year, JObject jo, string[] keys)
        {
            if (!jo.ContainsKey("imdb_id") && !jo.ContainsKey("original_title") && !jo.ContainsKey("original_name"))
                return;

            if (keys.Length == 1 && keys[0] == "default")
                keys = defaultInjectskeys;

            #region Поиск карточки в TMDB
            string imdbId = null;
            if (jo.ContainsKey("imdb_id"))
                imdbId = jo["imdb_id"]?.ToString();

            var header = HeadersModel.Init(("localrequest", AppInit.rootPasswd));

            long id = 0;
            string cat = string.Empty;

            if (!string.IsNullOrEmpty(imdbId) && imdbId.StartsWith("tt"))
            {
                var find = await Http.Get<JObject>($"http://{AppInit.conf.listen.localhost}:{AppInit.conf.listen.port}/tmdb/api/3/find/{imdbId}?external_source=imdb_id&api_key={AppInit.conf.tmdb.api_key}", timeoutSeconds: 5, headers: header);
                if (find != null)
                {
                    foreach (string key in new string[] { "movie_results", "tv_results" })
                    {
                        if (find.ContainsKey(key))
                        {
                            var movies = find[key] as JArray;
                            if (movies != null && movies.Count > 0)
                            {
                                id = movies[0].Value<long>("id");
                                cat = key == "movie_results" ? "movie" : "tv";
                                break;
                            }
                        }
                    }
                }
            }
            else if (jo.ContainsKey("original_title") || jo.ContainsKey("original_name"))
            {
                string originalTitle = jo["original_title"]?.ToString() ?? jo["original_name"]?.ToString();
                string type = jo.ContainsKey("original_title") ? "movie" : "tv";

                if (!string.IsNullOrEmpty(originalTitle) && int.TryParse(year, out int _year) && _year > 0)
                {
                    var searchMovie = await Http.Get<JObject>($"http://{AppInit.conf.listen.localhost}:{AppInit.conf.listen.port}/tmdb/api/3/search/{type}?query={HttpUtility.UrlEncode(originalTitle)}&api_key={AppInit.conf.tmdb.api_key}", timeoutSeconds: 5, headers: header);
                    if (searchMovie != null && searchMovie.ContainsKey("results"))
                    {
                        var results = searchMovie["results"] as JArray;
                        if (results != null && results.Count > 0)
                        {
                            long foundId = 0;
                            for (int i = 0; i < results.Count; i++)
                            {
                                var item = results[i] as JObject;
                                if (item == null)
                                    continue;

                                string date = item.Value<string>("release_date") ?? item.Value<string>("first_air_date");
                                if (string.IsNullOrEmpty(date))
                                    continue;

                                // date is usually in format YYYY-MM-DD, take first 4 chars
                                string yearStr = date.Length >= 4 ? date.Substring(0, 4) : date;
                                if (int.TryParse(yearStr, out int itemYear) && (itemYear == _year || itemYear == _year+1 || itemYear == _year-1))
                                {
                                    foundId = item.Value<long>("id");
                                    break;
                                }
                            }

                            if (foundId != 0)
                            {
                                id = foundId;
                                cat = type;
                            }
                        }
                    }
                }
            }
            #endregion

            if (id == 0)
                return;

            string append = "content_ratings,release_dates,external_ids,keywords,alternative_titles,videos,credits,recommendations,similar";
            var result = await Http.Get<JObject>($"http://{AppInit.conf.listen.localhost}:{AppInit.conf.listen.port}/tmdb/api/3/{cat}/{id}?api_key={AppInit.conf.tmdb.api_key}&append_to_response={append}&language=ru", timeoutSeconds: 5, headers: header);
            if (result == null)
                return;

            foreach (string key in keys)
            {
                if (key is "videos" or "recommendations" or "similar")
                {
                    if (result.ContainsKey(key) && result[key] is JObject _jo && _jo.ContainsKey("results"))
                        jo[key] = _jo["results"];
                }
                else if (result.ContainsKey(key))
                {
                    jo[key] = result[key];
                }
            }

            if (result.ContainsKey("id"))
                jo["tmdb_id"] = result["id"];

            if (!jo.ContainsKey("imdb_id") && result.ContainsKey("external_ids") && result["external_ids"] is JObject extIds && extIds.ContainsKey("imdb_id"))
                jo["imdb_id"] = extIds["imdb_id"];

            foreach (string key in addEmptykeys)
            {
                if (!jo.ContainsKey(key) && result.ContainsKey(key))
                {
                    var tok = result[key];
                    if (tok == null)
                        continue;

                    if (tok.Type == JTokenType.String)
                    {
                        var str = tok.Value<string>();
                        if (!string.IsNullOrWhiteSpace(str))
                            jo[key] = str;
                    }
                    else
                    {
                        jo[key] = tok;
                    }
                }
            }
        }
        #endregion
    }
}

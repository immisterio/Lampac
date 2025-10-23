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
                return ContentTo(rch.connectionMsg);

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

                var jo = new JObject()
                {
                    ["id"] = CrypTo.md5($"{plugin}:{uri}"),
                    ["url"] = $"{host}/catalog/card?plugin={plugin}&uri={HttpUtility.UrlEncode(uri)}&type={type}",
                    ["img"] = PosterApi.Size(host, ModInit.nodeValue(node, parse.image, host)?.ToString()),

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
                            jo[arg.name_arg] = JToken.FromObject(val);
                    }
                }

                if (!jo.ContainsKey("tagline") && !string.IsNullOrEmpty(original_name))
                    jo["tagline"] = original_name;

                if (init.tmdb_injects != null && init.tmdb_injects.Length > 0)
                    await Injects(jo, init.tmdb_injects);

                return ContentTo(JsonConvert.SerializeObject(jo));
            });
        }


        async Task Injects(JObject jo, string[] keys)
        {
            if (!jo.ContainsKey("imdb_id"))
                return;

            string imdbId = jo["imdb_id"]?.ToString();
            if (string.IsNullOrEmpty(imdbId) || !imdbId.StartsWith("tt"))
                return;

            var header = HeadersModel.Init(("localrequest", AppInit.rootPasswd));

            var find = await Http.Get<JObject>($"http://{AppInit.conf.listen.localhost}:{AppInit.conf.listen.port}/tmdb/api/3/find/{imdbId}?external_source=imdb_id&api_key={AppInit.conf.tmdb.api_key}&language=ru", timeoutSeconds: 5, headers: header);
            if (find == null)
                return;

            long id = 0;
            string cat = "";

            foreach (string key in new string[] { "movie_results", "tv_results" })
            {
                if (find.ContainsKey(key))
                {
                    var movies = find[key] as JArray;
                    if (movies != null && movies.Count > 0)
                    {
                        id = movies[0].Value<long>("id");
                        cat = key == "movie_results" ? "movie" : "tv";
                    }
                }
            }

            if (id == 0)
                return;

            var result = await Http.Get<JObject>($"http://{AppInit.conf.listen.localhost}:{AppInit.conf.listen.port}/tmdb/api/3/{cat}/{id}?api_key={AppInit.conf.tmdb.api_key}&append_to_response=content_ratings,release_dates,keywords,alternative_titles&language=ru", timeoutSeconds: 5, headers: header);
            if (result == null)
                return;

            foreach (string key in keys)
            {
                if (result.ContainsKey(key))
                    jo[key] = result[key];
            }

            if (result.ContainsKey("id"))
                jo["tmdb_id"] = result["id"];
        }
    }
}

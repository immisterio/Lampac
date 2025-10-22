using Microsoft.AspNetCore.Mvc;
using Shared.PlaywrightCore;

namespace Catalog.Controllers
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("catalog/list")]
        async public ValueTask<ActionResult> Index(string query, string plugin, string cat, string sort, int page = 1)
        {
            var init = ModInit.goInit(plugin)?.Clone();
            if (init == null || !init.enable)
                return BadRequest("init not found");

            if (!string.IsNullOrEmpty(query) && string.IsNullOrEmpty(init.search?.uri))
                return BadRequest("search disable");

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string search = query;
            string memKey = $"catalog:{plugin}:{search}:{sort}:{cat}:{page}";

            return await InvkSemaphore(init, memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out (List<PlaylistItem> playlists, int total_pages) cache, inmemory: false))
                {
                    #region contentParse
                    var contentParse = init.list?.contentParse ?? init.contentParse;

                    if (!string.IsNullOrEmpty(search) && init.search?.contentParse != null)
                        contentParse = init.search.contentParse;
                    #endregion

                    #region html
                    string url = $"{init.host}/{(page == 1 && init.list?.firstpage != null ? init.list?.firstpage : init.list?.uri)}";

                    if (!string.IsNullOrEmpty(search))
                    {
                        string uri = page == 1 && init.search?.firstpage != null ? init.search.firstpage : init.search?.uri;
                        url = $"{init.host}/{uri}".Replace("{search}", HttpUtility.UrlEncode(search));
                    }
                    else if (!string.IsNullOrEmpty(cat))
                    {
                        var menu = init.menu.FirstOrDefault(i => i.categories.Values.Contains(cat));
                        if (menu == null)
                            return BadRequest("menu");

                        string getFormat(string key)
                        {
                            if (menu.format.TryGetValue(key, out string _f))
                                return _f;

                            return string.Empty;
                        }

                        string eval = $"return (cat != null && sort != null) ? $\"{getFormat("sort")}\" : \"{getFormat("-")}\";";
                        url = CSharpEval.BaseExecute<string>(eval, new CatalogGlobalsMenuRoute(init.host, plugin, url, search, cat, sort, HttpContext.Request.Query, page));
                        
                        if (!url.StartsWith("http"))
                            url = $"{init.host}/{url}";
                    }

                    if (init.routeEval != null)
                        url = CSharpEval.Execute<string>(init.routeEval, new CatalogGlobalsMenuRoute(init.host, plugin, url, search, cat, sort, HttpContext.Request.Query, page));

                    reset:
                    string html =
                        rch.enable ? await rch.Get(url.Replace("{page}", page.ToString()), httpHeaders(init))
                        : init.priorityBrowser == "playwright" ? await PlaywrightBrowser.Get(init, url.Replace("{page}", page.ToString()), httpHeaders(init), proxy.data, cookies: init.cookies)
                        : await Http.Get(url.Replace("{page}", page.ToString()), headers: httpHeaders(init), proxy: proxy.proxy, timeoutSeconds: init.timeout);
                    #endregion

                    #region HtmlDocument
                    var doc = new HtmlDocument();

                    if (html != null)
                        doc.LoadHtml(html);
                    #endregion

                    cache.playlists = goPlaylist(cat, doc, requestInfo, host, contentParse, init, html, plugin);

                    if (cache.playlists == null || cache.playlists.Count == 0)
                    {
                        if (ModInit.IsRhubFallback(init))
                            goto reset;

                        if (!rch.enable)
                            proxyManager.Refresh();

                        return BadRequest("playlists");
                    }

                    if (contentParse.total_pages != null)
                    {
                        string _p = ModInit.nodeValue(doc.DocumentNode, contentParse.total_pages, host)?.ToString() ?? "";
                        if (int.TryParse(_p, out int _pages) && _pages > 0)
                            cache.total_pages = _pages;
                    }

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(memKey, cache, cacheTime(init.cache_time, init: init), inmemory: false);
                }

                #region total_pages
                int total_pages = init.list?.total_pages ?? 0;

                if (search != null && init.search != null)
                    total_pages = init.search.total_pages;

                if (total_pages == 0)
                    total_pages = cache.total_pages;
                #endregion

                #region results
                var results = new JArray();

                foreach (var pl in cache.playlists)
                {
                    var jo = new JObject()
                    {
                        ["id"] = pl.id,
                        ["img"] = pl.img,
                        ["method"] = pl.card
                    };

                    if (pl.is_serial)
                    {
                        jo["first_air_date"] = pl.year;
                        jo["name"] = pl.title;
                        
                        if (!string.IsNullOrEmpty(pl.original_title))
                            jo["original_name"] = pl.original_title;
                    }
                    else
                    {
                        jo["release_date"] = pl.year;
                        jo["title"] = pl.title;

                        if (!string.IsNullOrEmpty(pl.original_title))
                            jo["original_title"] = pl.original_title;
                    }

                    if (pl.args != null)
                    {
                        foreach (var a in pl.args)
                            jo[a.Key] = JToken.FromObject(a.Value);
                    }

                    results.Add(jo);
                }
                #endregion

                return ContentTo(JsonConvert.SerializeObject(new 
                {
                    page,
                    results,
                    total_pages
                }));
            });
        }


        #region goPlaylist
        static List<PlaylistItem> goPlaylist(string cat, HtmlDocument doc, in RequestModel requestInfo, string host, ContentParseSettings parse, CatalogSettings init, in string html, string plugin)
        {
            if (parse == null || string.IsNullOrEmpty(html))
                return null;

            if (init.debug)
                Console.WriteLine(html);

            string eval = parse.eval;
            if (!string.IsNullOrEmpty(eval) && eval.EndsWith(".cs"))
                eval = FileCache.ReadAllText($"catalog/sites/{eval}");

            if (string.IsNullOrEmpty(parse.nodes))
            {
                if (string.IsNullOrEmpty(eval))
                    return null;

                var options = ScriptOptions.Default
                    .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll"))
                    .AddImports("Shared")
                    .AddImports("Shared.Models")
                    .AddImports("Shared.Engine")
                    .AddReferences(CSharpEval.ReferenceFromFile("HtmlAgilityPack.dll"))
                    .AddImports("HtmlAgilityPack");

                return CSharpEval.Execute<List<PlaylistItem>>(eval, new CatalogPlaylist(init, plugin, host, html, doc, new List<PlaylistItem>()), options);
            }

            var nodes = doc.DocumentNode.SelectNodes(parse.nodes);
            if (nodes == null || nodes.Count == 0)
                return null;

            var playlists = new List<PlaylistItem>(nodes.Count);

            foreach (var node in nodes)
            {
                string name = ModInit.nodeValue(node, parse.name, host)?.ToString();
                string original_name = ModInit.nodeValue(node, parse.original_name, host)?.ToString();
                string href = ModInit.nodeValue(node, parse.href, host)?.ToString();
                string img = ModInit.nodeValue(node, parse.image, host)?.ToString();
                string year = ModInit.nodeValue(node, parse.year, host)?.ToString();

                if (init.debug)
                    Console.WriteLine($"\n\nname: {name}\nhref: {href}\nimg: {img}\nyear: {year}\n\n{node.OuterHtml}");

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(href))
                {
                    #region href
                    if (href.StartsWith("../"))
                        href = href.Replace("../", "");
                    else if (href.StartsWith("//"))
                        href = Regex.Replace(href, "//[^/]+/", "");
                    else if (href.StartsWith("http"))
                        href = Regex.Replace(href, "https?://[^/]+/", "");
                    else if (href.StartsWith("/"))
                        href = href.Substring(1);
                    #endregion

                    #region img
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

                    if (!init.ignore_no_picture && string.IsNullOrEmpty(img))
                        continue;

                    string clearText(string text)
                    {
                        if (string.IsNullOrEmpty(text))
                            return text;

                        text = text.Replace("&nbsp;", "");
                        return Regex.Replace(text, "<[^>]+>", "");
                    }

                    #region is_serial
                    bool? is_serial = null;

                    if (cat != null)
                    {
                        if (init.movies != null && init.movies.Contains(cat))
                            is_serial = false;
                        else if (init.serials != null && init.serials.Contains(cat))
                            is_serial = true;
                    }

                    if (is_serial == null && parse.serial_regex != null)
                        is_serial = Regex.IsMatch(node.OuterHtml, parse.serial_regex, RegexOptions.IgnoreCase);
                    #endregion

                    var pl = new PlaylistItem()
                    {
                        id = CrypTo.md5($"{plugin}:{href}"),
                        title = clearText(name),
                        original_title = clearText(original_name),
                        img = PosterApi.Size(host, img),
                        year = clearText(year),
                        card = $"{host}/catalog/card?plugin={plugin}&uri={HttpUtility.UrlEncode(href)}&type={(is_serial == true ? "tv" : "movie")}",
                        is_serial = is_serial == true
                    };

                    if (parse.args != null)
                    {
                        foreach (var arg in parse.args)
                        {
                            object val = ModInit.nodeValue(node, arg, host);
                            if (val != null)
                            {
                                if (pl.args == null)
                                    pl.args = new Dictionary<string, object>();

                                pl.args[arg.name_arg] = val;
                            }
                        }
                    }

                    if (eval != null)
                    {
                        var options = ScriptOptions.Default
                            .AddReferences(CSharpEval.ReferenceFromFile("Shared.dll"))
                            .AddImports("Shared")
                            .AddImports("Shared.Models")
                            .AddImports("Shared.Engine")
                            .AddReferences(CSharpEval.ReferenceFromFile("HtmlAgilityPack.dll"))
                            .AddImports("HtmlAgilityPack");

                        pl = CSharpEval.Execute<PlaylistItem>(eval, new CatalogChangePlaylis(init, plugin, host, html, nodes, pl, node), options);
                    }

                    if (pl != null)
                        playlists.Add(pl);
                }
            }

            return playlists;
        }
        #endregion
    }
}

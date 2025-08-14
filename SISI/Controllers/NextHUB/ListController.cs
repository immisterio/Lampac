using HtmlAgilityPack;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Shared.Engine;
using Shared.Engine.CORE;
using Shared.Model.Online;
using Shared.Model.SISI;
using Shared.Model.SISI.NextHUB;
using Shared.Models.CSharpGlobals;
using Shared.PlaywrightCore;
using SISI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Lampac.Controllers.NextHUB
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("nexthub")]
        async public ValueTask<ActionResult> Index(string plugin, string search, string sort, string cat, string model, int pg = 1)
        {
            if (!AppInit.conf.sisi.NextHUB)
                return OnError("disabled");

            var init = Root.goInit(plugin);
            if (init == null)
                return OnError("init not found", rcache: false);

            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (!string.IsNullOrEmpty(search) && string.IsNullOrEmpty(init.search?.uri))
                return OnError("search disable");

            string memKey = $"nexthub:{plugin}:{search}:{sort}:{cat}:{model}:{pg}";
            if (init.menu?.customs != null)
            {
                foreach (var item in init.menu.customs)
                    memKey += $":{HttpContext.Request.Query[item.arg]}";
            }

            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager(init);
                var proxy = proxyManager.BaseGet();

                #region html
                string url = $"{init.host}/{(pg == 1 && init.list.firstpage != null ? init.list.firstpage : init.list.uri)}";
                if (!string.IsNullOrEmpty(search))
                {
                    string uri = pg == 1 && init.search?.firstpage != null ? init.search.firstpage : init.search?.uri;
                    url = $"{init.host}/{uri}".Replace("{search}", HttpUtility.UrlEncode(search));
                }
                else
                {
                    if (!string.IsNullOrEmpty(sort))
                        url = $"{init.host}/{sort}";
                    else if (!string.IsNullOrEmpty(cat))
                        url = $"{init.host}/{init.menu.formatcat(cat)}";
                    else if (!string.IsNullOrEmpty(model))
                        url = $"{init.host}/{model}";
                    else if (init.menu?.customs != null)
                    {
                        foreach (var c in init.menu.customs)
                        {
                            if (HttpContext.Request.Query.ContainsKey(c.arg))
                                url = $"{init.host}/{c.format.Replace("{value}", HttpContext.Request.Query[c.arg])}";
                        }
                    }

                    if (init.menu?.route != null)
                    {
                        string goroute(string name)
                        {
                            if (init.menu.route.TryGetValue(name, out string value))
                                return value;

                            if (init.menu.route.TryGetValue("-", out value))
                                return value;

                            return string.Empty;
                        }

                        string eval = $"return (cat != null && sort != null) ? $\"{goroute("catsort")}\" : (model != null && sort != null) ? $\"{goroute("modelsort")}\" : model != null ? $\"{goroute("model")}\" : cat != null ? $\"{goroute("cat")}\" : sort != null ? $\"{goroute("sort")}\" : \"{url}\";";
                        url = CSharpEval.Execute<string>(eval, new NxtMenuRoute(init.host, plugin, url, search, cat, sort, model, HttpContext.Request.Query, pg));
                    }
                }

                if (init.route?.eval != null)
                    url = CSharpEval.Execute<string>(init.route.eval, new NxtMenuRoute(init.host, plugin, url, search, cat, sort, model, HttpContext.Request.Query, pg));

                string html = init.priorityBrowser == "http" ? await HttpClient.Get(url.Replace("{page}", pg.ToString()), headers: httpHeaders(init), proxy: proxy.proxy, timeoutSeconds: 8) :
                              init.list.viewsource ? await PlaywrightBrowser.Get(init, url.Replace("{page}", pg.ToString()), httpHeaders(init), proxy.data, cookies: init.cookies) :
                                                     await ContentAsync(init, url.Replace("{page}", pg.ToString()), httpHeaders(init), proxy.data, search, sort, cat, pg);

                if (string.IsNullOrEmpty(html))
                    return OnError("html", rcache: !init.debug);
                #endregion

                #region contentParse
                var contentParse = init.list.contentParse ?? init.contentParse;

                if (!string.IsNullOrEmpty(search) && init.search?.contentParse != null)
                    contentParse = init.search.contentParse;

                if (!string.IsNullOrEmpty(model) && init.modelParse != null)
                    contentParse = init.modelParse;
                #endregion

                playlists = goPlaylist(host, contentParse, init, html, plugin);

                if (playlists == null || playlists.Count == 0)
                    return OnError("playlists", proxyManager, rcache: !init.debug);

                proxyManager.Success();
                hybridCache.Set(memKey, playlists, cacheTime(init.cache_time, init: init));
            }


            List<MenuItem> menu = new List<MenuItem>(3);
            bool usedRoute = init.menu?.route != null || init.route?.eval != null;

            #region search
            if (string.IsNullOrEmpty(model) && init.search?.uri != null)
            {
                menu.Add(new MenuItem()
                {
                    title = "Поиск",
                    search_on = "search_on",
                    playlist_url = $"{host}/nexthub?plugin={plugin}",
                });
            }
            #endregion

            #region sort
            if (string.IsNullOrEmpty(search) && init.menu?.sort != null)
            {
                var msort = new MenuItem()
                {
                    title = $"Сортировка: {init.menu.sort.FirstOrDefault(i => i.Value.Trim() == sort).Key ?? init.menu.sort.First().Key}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                };

                string arg = usedRoute && init.menu.bind ? $"&cat={HttpUtility.UrlEncode(cat)}&model={HttpUtility.UrlEncode(model)}" : string.Empty;

                foreach (var s in init.menu.sort)
                {
                    msort.submenu.Add(new MenuItem()
                    {
                        title = s.Key,
                        playlist_url = $"{host}/nexthub?plugin={plugin}&sort={HttpUtility.UrlEncode(s.Value.Trim())}" + arg,
                    });
                }

                if (msort.submenu.Count > 0)
                    menu.Add(msort);
            }
            #endregion

            #region categories
            if (string.IsNullOrEmpty(search) && string.IsNullOrEmpty(model) && init.menu?.categories != null)
            {
                var categories = init.menu.categories.Where(i => i.Key != "format");

                var mcat = new MenuItem()
                {
                    title = $"Категории: {categories.FirstOrDefault(i => i.Value.Trim() == cat).Key ?? "Выбрать"}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                };

                string arg = usedRoute && init.menu.bind ? $"&sort={HttpUtility.UrlEncode(sort)}" : string.Empty;

                foreach (var s in categories)
                {
                    mcat.submenu.Add(new MenuItem()
                    {
                        title = s.Key,
                        playlist_url = $"{host}/nexthub?plugin={plugin}&cat={HttpUtility.UrlEncode(s.Value.Trim())}" + arg,
                    });
                }

                if (mcat.submenu.Count > 0)
                    menu.Add(mcat);
            }
            #endregion

            #region custom categories
            if (string.IsNullOrEmpty(search) && string.IsNullOrEmpty(model) && init.menu?.customs != null)
            {
                foreach (var custom in init.menu.customs)
                {
                    string argvalue = HttpContext.Request.Query[custom.arg];

                    var mcat = new MenuItem()
                    {
                        title = $"{custom.name}: {custom.submenu.FirstOrDefault(i => i.Value.Trim() == argvalue).Key ?? "Выбрать"}",
                        playlist_url = "submenu",
                        submenu = new List<MenuItem>()
                    };

                    foreach (var s in custom.submenu)
                    {
                        mcat.submenu.Add(new MenuItem()
                        {
                            title = s.Key,
                            playlist_url = $"{host}/nexthub?plugin={plugin}&{custom.arg}={HttpUtility.UrlEncode(s.Value.Trim())}",
                        });
                    }

                    if (mcat.submenu.Count > 0)
                        menu.Add(mcat);
                }
            }
            #endregion

            int total_pages = init.list.total_pages;
            if (search != null)
                total_pages = init.search.total_pages;

            return OnResult(playlists, menu.Count == 0 ? null : menu, plugin: init.plugin, total_pages: total_pages);
        }


        #region goPlaylist
        public static List<PlaylistItem> goPlaylist(string host, ContentParseSettings parse, NxtSettings init, in string html, string plugin)
        {
            if (parse == null || string.IsNullOrEmpty(parse.nodes) || string.IsNullOrEmpty(html))
                return null;

            if (init.debug)
                Console.WriteLine(html);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var nodes = doc.DocumentNode.SelectNodes(parse.nodes);
            if (nodes == null || nodes.Count == 0)
                return null;

            var playlists = new List<PlaylistItem>(nodes.Count);

            string eval = parse.eval;
            if (!string.IsNullOrEmpty(eval) && eval.EndsWith(".cs"))
                eval = FileCache.ReadAllText($"NextHUB/sites/{eval}");

            foreach (var row in nodes)
            {
                #region nodeValue
                string nodeValue(SingleNodeSettings nd)
                {
                    if (nd != null)
                    {
                        if (string.IsNullOrEmpty(nd.node) && !string.IsNullOrEmpty(nd.attribute))
                        {
                            return row.GetAttributeValue(nd.attribute, null);
                        }
                        else
                        {
                            var inNode = row.SelectSingleNode(nd.node);
                            if (inNode != null)
                                return (!string.IsNullOrEmpty(nd.attribute) ? inNode.GetAttributeValue(nd.attribute, null) : inNode.InnerText)?.Trim();
                        }
                    }

                    return null;
                }
                #endregion

                string name = nodeValue(parse.name);
                string href = nodeValue(parse.href);
                string img = nodeValue(parse.img);
                string duration = nodeValue(parse.duration);
                string quality = nodeValue(parse.quality);
                string myarg = nodeValue(parse.myarg);
                string preview = nodeValue(parse.preview);

                #region model
                ModelItem? model = null;
                if (parse.model != null)
                {
                    string mname = nodeValue(parse.model.name);
                    string mhref = nodeValue(parse.model.href);

                    if (!string.IsNullOrEmpty(mname) && !string.IsNullOrEmpty(mhref))
                    {
                        model = new ModelItem()
                        {
                            name = mname,
                            uri = $"{host}/nexthub?plugin={plugin}&model={HttpUtility.UrlEncode(mhref)}"
                        };
                    }
                }
                #endregion

                if (init.debug)
                    Console.WriteLine($"\n\nname: {name}\nhref: {href}\nimg: {img}\nduration: {duration}\nquality: {quality}\nmyarg: {myarg}\n\n{row.OuterHtml}");

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(href))
                {
                    #region href
                    if (href.StartsWith("../"))
                        href = $"{init.host}/{href.Replace("../", "")}";
                    else if (href.StartsWith("//"))
                        href = $"https:{href}";
                    else if (href.StartsWith("/"))
                        href = init.host + href;
                    else if (!href.StartsWith("http"))
                        href = $"{init.host}/{href}";
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

                    #region preview
                    if (preview != null)
                    {
                        preview = preview.Replace("&amp;", "&").Replace("\\", "");

                        if (preview.StartsWith("../"))
                            preview = $"{init.host}/{preview.Replace("../", "")}";
                        else if (preview.StartsWith("//"))
                            preview = $"https:{preview}";
                        else if (preview.StartsWith("/"))
                            preview = init.host + preview;
                        else if (!preview.StartsWith("http"))
                            preview = $"{init.host}/{preview}";

                        if (init.streamproxy_preview)
                            preview = $"{host}/proxy/{ProxyLink.Encrypt(preview, string.Empty, verifyip: false)}";
                    }
                    #endregion

                    string clearText(string text)
                    {
                        if (string.IsNullOrEmpty(text))
                            return text;

                        text = text.Replace("&nbsp;", "");
                        return Regex.Replace(text, "<[^>]+>", "");
                    }

                    var pl = new PlaylistItem()
                    {
                        name = clearText(name),
                        video = $"{host}/nexthub/vidosik?uri={HttpUtility.UrlEncode($"{plugin}_-:-_{href}")}",
                        preview = preview,
                        picture = img,
                        time = clearText(duration),
                        quality = clearText(quality),
                        myarg = myarg,
                        json = true,
                        related = init.view != null ? init.view.related : false,
                        model = model,
                        bookmark = new Bookmark()
                        {
                            site = "nexthub",
                            href = $"{plugin}_-:-_{href}",
                            image = img
                        }
                    };

                    if (eval != null)
                        pl = CSharpEval.Execute<PlaylistItem>(eval, new NxtChangePlaylis(html, host, init, pl, nodes, row));

                    if (pl != null)
                        playlists.Add(pl);
                }
            }

            return playlists;
        }
        #endregion

        #region ContentAsync
        async Task<string> ContentAsync(NxtSettings init, string url, List<HeadersModel> headers, (string ip, string username, string password) proxy, string search, string sort, string cat, int pg)
        {
            try
            {
                var conf = string.IsNullOrEmpty(search) ? init.list : init.search;

                using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                {
                    var page = await browser.NewPageAsync(init.plugin, headers?.ToDictionary(), proxy: proxy, keepopen: init.keepopen).ConfigureAwait(false);
                    if (page == default)
                        return null;

                    if (init.cookies != null)
                        await page.Context.AddCookiesAsync(init.cookies).ConfigureAwait(false);

                    string routeEval = conf.routeEval;
                    if (!string.IsNullOrEmpty(routeEval) && routeEval.EndsWith(".cs"))
                        routeEval = FileCache.ReadAllText($"NextHUB/sites/{routeEval}");

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            #region routeEval
                            if (routeEval != null)
                            {
                                bool _next = await CSharpEval.ExecuteAsync<bool>(routeEval, new NxtRoute(route, search, sort, cat, pg));
                                if (!_next)
                                    return;
                            }
                            #endregion

                            if (conf.patternAbort != null && Regex.IsMatch(route.Request.Url, conf.patternAbort, RegexOptions.IgnoreCase))
                            {
                                PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                await route.AbortAsync();
                                return;
                            }

                            if (init.abortMedia || init.fullCacheJS)
                            {
                                if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: init.abortMedia, fullCacheJS: init.fullCacheJS))
                                    return;
                            }
                            else
                            {
                                PlaywrightBase.ConsoleLog($"Playwright: {route.Request.Method} {route.Request.Url}");
                            }

                            await browser.ClearContinueAsync(route, page);
                        }
                        catch (Exception ex) { PlaywrightBase.ConsoleLog(ex.Message); }
                    });

                    string content = null;
                    PlaywrightBase.GotoAsync(page, url);

                    if (!string.IsNullOrEmpty(conf.waitForSelector))
                    {
                        try
                        {
                            await page.WaitForSelectorAsync(conf.waitForSelector, new PageWaitForSelectorOptions
                            {
                                Timeout = conf.waitForSelector_timeout

                            }).ConfigureAwait(false);
                        }
                        catch { }

                        content = await page.ContentAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
                        content = await page.ContentAsync().ConfigureAwait(false);
                    }

                    PlaywrightBase.WebLog("GET", url, content, proxy);
                    return content;
                }
            }
            catch
            {
                return null;
            }
        }
        #endregion
    }
}

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
        async public ValueTask<ActionResult> Index(string plugin, string search, string sort, string cat, int pg = 1)
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

            string memKey = $"nexthub:{plugin}:{search}:{sort}:{cat}:{pg}";
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
                else if (!string.IsNullOrEmpty(sort))
                    url = $"{init.host}/{sort}";

                else if (!string.IsNullOrEmpty(cat))
                    url = $"{init.host}/{cat}";

                string html = init.priorityBrowser == "http" ? await HttpClient.Get(url.Replace("{page}", pg.ToString()), headers: httpHeaders(init), proxy: proxy.proxy, timeoutSeconds: 8) :
                              init.list.viewsource ? await PlaywrightBrowser.Get(init, url.Replace("{page}", pg.ToString()), httpHeaders(init), proxy.data, cookies: init.cookies) :
                                                     await ContentAsync(init, url.Replace("{page}", pg.ToString()), httpHeaders(init), proxy.data, search, sort, cat, pg);

                if (string.IsNullOrEmpty(html))
                    return OnError("html", rcache: !init.debug);
                #endregion

                var contentParse = init.list.contentParse ?? init.contentParse;
                if (!string.IsNullOrEmpty(search))
                    contentParse = init.search?.contentParse ?? init.contentParse;

                playlists = goPlaylist(host, contentParse, init, html, plugin);

                if (playlists == null || playlists.Count == 0)
                    return OnError("playlists", proxyManager, rcache: !init.debug);

                proxyManager.Success();
                hybridCache.Set(memKey, playlists, cacheTime(init.cache_time, init: init));
            }


            List<MenuItem> menu = new List<MenuItem>(3);

            #region search
            if (init.search?.uri != null)
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
                    title = $"Сортировка: {init.menu.sort.FirstOrDefault(i => i.Value == sort).Key ?? init.menu.sort.First().Key}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                };

                foreach (var s in init.menu.sort)
                {
                    msort.submenu.Add(new MenuItem()
                    {
                        title = s.Key,
                        playlist_url = $"{host}/nexthub?plugin={plugin}&sort={HttpUtility.UrlEncode(s.Value)}",
                    });
                }

                if (msort.submenu.Count > 0)
                    menu.Add(msort);
            }
            #endregion

            #region categories
            if (string.IsNullOrEmpty(search) && init.menu?.categories != null)
            {
                var mcat = new MenuItem()
                {
                    title = $"Категории: {init.menu.categories.FirstOrDefault(i => i.Value == cat).Key ?? init.menu.categories.First().Key}",
                    playlist_url = "submenu",
                    submenu = new List<MenuItem>()
                };

                foreach (var s in init.menu.categories)
                {
                    mcat.submenu.Add(new MenuItem()
                    {
                        title = s.Key,
                        playlist_url = $"{host}/nexthub?plugin={plugin}&cat={HttpUtility.UrlEncode(s.Value)}",
                    });
                }

                if (mcat.submenu.Count > 0)
                    menu.Add(mcat);
            }
            #endregion

            return OnResult(playlists, menu.Count == 0 ? null : menu, plugin: init.plugin);
        }


        #region goPlaylist
        public static List<PlaylistItem> goPlaylist(string host, ContentParseSettings parse, NxtSettings init, string html, string plugin)
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
            string eval = string.IsNullOrEmpty(parse.eval) ? null : FileCache.ReadAllText($"NextHUB/{parse.eval}");

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

                    var pl = new PlaylistItem()
                    {
                        name = clearText(name),
                        video = $"{host}/nexthub/vidosik?uri={HttpUtility.UrlEncode($"{plugin}_-:-_{href}")}",
                        picture = img,
                        time = clearText(duration),
                        quality = clearText(quality),
                        myarg = myarg,
                        json = true,
                        related = init.view != null ? init.view.related : false,
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

                    string routeEval = null;
                    if (conf.routeEval != null)
                        routeEval = FileCache.ReadAllText($"NextHUB/{conf.routeEval}");

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

                            await route.ContinueAsync();
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

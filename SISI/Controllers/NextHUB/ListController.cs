using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Models.SISI;
using Shared.Engine.CORE;
using SISI;
using Newtonsoft.Json;
using Shared.Model.SISI.NextHUB;
using Shared.PlaywrightCore;
using Shared.Model.SISI;
using HtmlAgilityPack;
using System.Web;
using Microsoft.Playwright;
using Shared.Engine;
using Shared.Model.Online;
using System.Linq;
using System.Text.RegularExpressions;
using System;
using Lampac.Engine.CORE;

namespace Lampac.Controllers.NextHUB
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("nexthub")]
        async public Task<ActionResult> Index(string plugin, string search, string sort, string cat, int pg = 1)
        {
            if (!AppInit.conf.sisi.NextHUB)
                return OnError("disabled");

            var init = JsonConvert.DeserializeObject<NxtSettings>($"{{{FileCache.ReadAllText($"NextHUB/{plugin}.json")}}}");
            if (string.IsNullOrEmpty(init.plugin))
                init.plugin = init.displayname;

            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            string memKey = $"nexthub:{plugin}:{search}:{sort}:{cat}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager(init);
                var proxy = proxyManager.BaseGet();

                #region html
                string url = $"{init.host}/{init.list.uri}";
                if (!string.IsNullOrEmpty(search))
                    url = $"{init.host}/{init.search.uri}".Replace("{search}", HttpUtility.UrlEncode(search));

                else if (!string.IsNullOrEmpty(sort))
                    url = $"{init.host}/{sort}";

                else if (!string.IsNullOrEmpty(cat))
                    url = $"{init.host}/{cat}";

                string html = init.priorityBrowser == "http" ? await HttpClient.Get(url.Replace("{page}", pg.ToString()), headers: httpHeaders(init), proxy: proxy.proxy, timeoutSeconds: 8) :
                              init.list.viewsource ? await PlaywrightBrowser.Get(init, url.Replace("{page}", pg.ToString()), httpHeaders(init), proxy.data, cookies: init.cookies) :
                                                     await ContentAsync(init, url.Replace("{page}", pg.ToString()), httpHeaders(init), proxy.data, !string.IsNullOrEmpty(search));

                if (string.IsNullOrEmpty(html))
                    return OnError("html");
                #endregion

                var contentParse = init.list.contentParse ?? init.contentParse;
                if (!string.IsNullOrEmpty(search))
                    contentParse = init.search.contentParse ?? init.contentParse;

                playlists = goPlaylist(host, contentParse, init, html, plugin);

                if (playlists == null || playlists.Count == 0)
                    return OnError("playlists", proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, playlists, cacheTime(init.cache_time, init: init));
            }


            List<MenuItem> menu = new List<MenuItem>();

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
            if (parse == null || string.IsNullOrEmpty(parse.nodes))
                return null;

            if (init.debug)
                Console.WriteLine(html);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var nodes = doc.DocumentNode.SelectNodes(parse.nodes);
            if (nodes == null || nodes.Count == 0)
                return null;

            var playlists = new List<PlaylistItem>() { Capacity = nodes.Count };

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

                if (init.debug)
                    Console.WriteLine($"\n\nname: {name}\nhref: {href}\nimg: {img}\nduration: {duration}\nquality: {quality}\n\n{row.OuterHtml}");

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(href))
                {
                    if (href.StartsWith("//"))
                        href = $"https:{href}";
                    else if (href.StartsWith("/"))
                        href = init.host + href;

                    if (img != null)
                    {
                        if (img.StartsWith("//"))
                            img = $"https:{img}";
                        else if (img.StartsWith("/"))
                            img = init.host + img;
                    }

                    if (!init.ignore_no_picture && string.IsNullOrEmpty(img))
                        continue;

                    var pl = new PlaylistItem()
                    {
                        name = name,
                        video = $"{host}/nexthub/vidosik?uri={HttpUtility.UrlEncode($"{plugin}_-:-_{href}")}",
                        picture = img,
                        time = duration,
                        json = true,
                        related = init.view.related,
                        bookmark = new Bookmark()
                        {
                            site = "nexthub",
                            href = $"{plugin}_-:-_{href}",
                            image = img
                        }
                    };

                    playlists.Add(pl);
                }
            }

            return playlists;
        }
        #endregion

        #region ContentAsync
        async ValueTask<string> ContentAsync(NxtSettings init, string url, List<HeadersModel> headers, (string ip, string username, string password) proxy, bool isSearch = false)
        {
            try
            {
                var conf = isSearch ? init.search : init.list;

                using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                {
                    var page = await browser.NewPageAsync(init.plugin, headers?.ToDictionary(), proxy: proxy, keepopen: init.keepopen);
                    if (page == null)
                        return null;

                    if (init.cookies != null)
                        await page.Context.AddCookiesAsync(init.cookies);

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            if (conf.patternAbort != null && Regex.IsMatch(route.Request.Url, conf.patternAbort, RegexOptions.IgnoreCase))
                            {
                                Console.WriteLine($"Playwright: Abort {route.Request.Url}");
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
                                Console.WriteLine($"Playwright: {route.Request.Method} {route.Request.Url}");
                            }

                            await route.ContinueAsync();
                        }
                        catch { }
                    });

                    PlaywrightBase.GotoAsync(page, url);
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    string content = await page.ContentAsync();

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

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

namespace Lampac.Controllers.NextHUB
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("nexthub")]
        async public Task<ActionResult> Index(string plugin, string search, string sort, string cat, int pg = 1)
        {
            var init = JsonConvert.DeserializeObject<NxtSettings>($"{{{FileCache.ReadAllText($"NextHUB/{plugin}.json")}}}");

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

                string html = init.list.viewsource ? await PlaywrightBrowser.Get(init, url.Replace("{page}", pg.ToString()), httpHeaders(init), proxy.data) :
                                                     await ContentAsync(init, url.Replace("{page}", pg.ToString()), httpHeaders(init), proxy.data);

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

            #region menu
            List<MenuItem> menu = null;

            if (init.search?.uri != null)
            {
                menu = new List<MenuItem>()
                {
                    new MenuItem()
                    {
                        title = "Поиск",
                        search_on = "search_on",
                        playlist_url = $"{host}/nexthub?plugin={plugin}",
                    }
                };

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
            }
            #endregion

            return OnResult(playlists, menu, plugin: init.plugin);
        }


        #region goPlaylist
        public static List<PlaylistItem> goPlaylist(string host, ContentParseSettings parse, NxtSettings init, string html, string plugin)
        {
            if (parse == null || string.IsNullOrEmpty(parse.nodes))
                return null;

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
                        var inNode = row.SelectSingleNode(nd.node);
                        if (inNode != null)
                            return (!string.IsNullOrEmpty(nd.attribute) ? inNode.GetAttributeValue(nd.attribute, null) : inNode.InnerText)?.Trim();
                    }

                    return null;
                }
                #endregion

                string name = nodeValue(parse.name);
                string href = nodeValue(parse.href);
                string img = nodeValue(parse.img);
                string duration = nodeValue(parse.duration);
                string quality = nodeValue(parse.quality);

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(href))
                {
                    if (href.StartsWith("/"))
                        href = init.host + href;

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
        async ValueTask<string> ContentAsync(NxtSettings init, string url, List<HeadersModel> headers, (string ip, string username, string password) proxy)
        {
            try
            {
                using (var browser = new PlaywrightBrowser())
                {
                    var page = await browser.NewPageAsync(init.plugin, headers?.ToDictionary(), proxy: proxy);
                    if (page == null)
                        return null;

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: init.list.abortMedia, fullCacheJS: init.list.fullCacheJS))
                                return;

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

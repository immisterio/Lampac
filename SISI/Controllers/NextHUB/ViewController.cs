using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;
using Shared.Model.Online;
using Shared.Engine;
using Shared.Model.SISI.NextHUB;
using Shared.PlaywrightCore;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.AspNetCore.Routing;
using HtmlAgilityPack;

namespace Lampac.Controllers.NextHUB
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("nexthub/vidosik")]
        async public Task<ActionResult> Index(string uri, bool related)
        {
            if (!AppInit.conf.sisi.NextHUB)
                return OnError("disabled");

            string plugin = uri.Split("_-:-_")[0];
            string url = uri.Split("_-:-_")[1];

            var init = RootController.goInit(plugin);
            if (init == null)
                return OnError("init not found");

            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            var video = await goVideo(plugin, url, init, proxyManager, proxy.data);
            if (string.IsNullOrEmpty(video.file))
                return OnError("file");

            var stream_links = new StreamItem()
            {
                qualitys = new Dictionary<string, string>()
                {
                    ["auto"] = video.file
                },
                recomends = video.recomends
            };

            if (related)
                return OnResult(stream_links?.recomends, null, plugin: plugin, total_pages: 1);

            var headers = video.headers;
            if (init.headers_stream != null)
                headers = httpHeaders(init.host, init.headers_stream);

            return OnResult(stream_links, init, proxy.proxy, headers: headers);
        }


        #region goVideo
        async ValueTask<(string file, List<HeadersModel> headers, List<PlaylistItem> recomends)> goVideo(string plugin, string url, NxtSettings init, ProxyManager proxyManager, (string ip, string username, string password) proxy)
        {
            if (string.IsNullOrEmpty(url))
                return default;

            try
            {
                string memKey = $"nexthub:view18:goVideo:{url}";
                if (init.view.bindingToIP)
                    memKey += $":{proxyManager.CurrentProxyIp}";

                if (!hybridCache.TryGetValue(memKey, out (string file, List<HeadersModel> headers, List<PlaylistItem> recomends) cache))
                {
                    using (var browser = new PlaywrightBrowser(init.view.priorityBrowser ?? init.priorityBrowser))
                    {
                        var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy, keepopen: init.view.keepopen);
                        if (page == null)
                            return default;

                        string browser_host = "." + Regex.Replace(init.host, "^https?://", "");

                        if (init.view.keepopen)
                            await page.Context.ClearCookiesAsync(new BrowserContextClearCookiesOptions { Domain = browser_host, Name = "cf_clearance" });

                        if (init.cookies != null)
                            await page.Context.AddCookiesAsync(init.cookies);

                        if (!string.IsNullOrEmpty(init.view.addInitScript))
                            await page.AddInitScriptAsync(init.view.addInitScript);

                        if (!string.IsNullOrEmpty(init.view.evaluate))
                        {
                            #region response
                            string html = null;
                            IResponse response = default;

                            if (init.view.NetworkIdle)
                            {
                                response = await page.GotoAsync(url);
                                if (response != null)
                                {
                                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                                    html = await page.ContentAsync();
                                }
                            }
                            else
                            {
                                if (browser.firefox != null)
                                {
                                    response = await page.GotoAsync(url, new PageGotoOptions() { WaitUntil = WaitUntilState.DOMContentLoaded });
                                }
                                else
                                {
                                    response = await page.GotoAsync($"view-source:{url}");
                                }

                                if (response != null)
                                    html = await response.TextAsync();
                            }

                            PlaywrightBase.WebLog(response.Request, response, html, proxy);
                            #endregion

                            #region evaluate
                            if (!string.IsNullOrEmpty(html))
                            {
                                string evaluate = init.view.evaluate;
                                if (evaluate.EndsWith(".js"))
                                    evaluate = System.IO.File.ReadAllText($"NextHUB/{init.view.evaluate}");

                                cache.file = await page.EvaluateAsync<string>($"(html, plugin, url) => {{ {evaluate} }}", new { html, plugin, url });
                                Console.WriteLine($"Playwright: SET {cache.file}");
                            }
                            #endregion
                        }
                        else
                        {
                            #region RouteAsync
                            await page.RouteAsync("**/*", async route =>
                            {
                                try
                                {
                                    if (browser.IsCompleted || (init.view.patternAbort != null && Regex.IsMatch(route.Request.Url, init.view.patternAbort, RegexOptions.IgnoreCase)))
                                    {
                                        Console.WriteLine($"Playwright: Abort {route.Request.Url}");
                                        await route.AbortAsync();
                                        return;
                                    }

                                    if (init.view.patternFile != null && Regex.IsMatch(route.Request.Url, init.view.patternFile, RegexOptions.IgnoreCase))
                                    {
                                        cache.headers = new List<HeadersModel>();
                                        foreach (var item in route.Request.Headers)
                                        {
                                            if (item.Key.ToLower() is "host" or "accept-encoding" or "connection")
                                                continue;

                                            cache.headers.Add(new HeadersModel(item.Key, item.Value.ToString()));
                                        }

                                        Console.WriteLine($"Playwright: SET {route.Request.Url}");
                                        browser.SetPageResult(route.Request.Url);
                                        await route.AbortAsync();
                                        return;
                                    }

                                    if (init.view.abortMedia || init.view.fullCacheJS)
                                    {
                                        if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: init.view.abortMedia, fullCacheJS: init.view.fullCacheJS))
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
                            #endregion

                            #region GotoAsync
                            string html = null;
                            var responce = await page.GotoAsync(url, new PageGotoOptions() { WaitUntil = WaitUntilState.DOMContentLoaded });
                            if (responce != null)
                                html = await responce.TextAsync();
                            #endregion

                            #region WaitForSelector
                            if (!string.IsNullOrEmpty(init.view.waitForSelector) || !string.IsNullOrEmpty(init.view.playbtn))
                            {
                                try
                                {
                                    await page.WaitForSelectorAsync(init.view.waitForSelector ?? init.view.playbtn, new PageWaitForSelectorOptions
                                    {
                                        Timeout = init.view.waitForSelector_timeout
                                    });
                                }
                                catch { }
                            }
                            #endregion

                            if (!string.IsNullOrEmpty(init.view.playbtn))
                                await page.ClickAsync(init.view.playbtn);

                            if (init.view.nodeFile != null)
                            {
                                #region nodeFile
                                string goFile(string _content)
                                {
                                    if (!string.IsNullOrEmpty(_content))
                                    {
                                        var doc = new HtmlDocument();
                                        doc.LoadHtml(_content);
                                        var videoNode = doc.DocumentNode.SelectSingleNode(init.view.nodeFile.node);
                                        if (videoNode != null)
                                            return (!string.IsNullOrEmpty(init.view.nodeFile.attribute) ? videoNode.GetAttributeValue(init.view.nodeFile.attribute, null) : videoNode.InnerText)?.Trim();
                                    }

                                    return null;
                                }

                                if (init.view.NetworkIdle)
                                {
                                    for (int i = 0; i < 10; i++)
                                    {
                                        cache.file = goFile(await page.ContentAsync());
                                        if (!string.IsNullOrEmpty(cache.file))
                                            break;

                                        Console.WriteLine("ContentAsync: " + (i+1));
                                        await Task.Delay(800);
                                    }
                                }
                                else
                                {
                                    cache.file = goFile(html);
                                }
                                #endregion
                            }
                            else
                            {
                                cache.file = await browser.WaitPageResult();
                            }

                            #region related
                            if (!string.IsNullOrEmpty(cache.file))
                            {
                                if (init.view.related)
                                {
                                    if (init.view.NetworkIdle)
                                    {
                                        string contetnt = await page.ContentAsync();
                                        cache.recomends = ListController.goPlaylist(host, init.view.contentParse ?? init.contentParse, init, contetnt, plugin);
                                    }
                                    else
                                    {
                                        cache.recomends = ListController.goPlaylist(host, init.view.contentParse ?? init.contentParse, init, html, plugin);
                                    }
                                }
                            }
                            #endregion
                        }
                    }

                    if (string.IsNullOrEmpty(cache.file))
                    {
                        proxyManager.Refresh();
                        return default;
                    }

                    cache.file = cache.file.Replace("\\", "").Replace("&amp;", "&");

                    proxyManager.Success();
                    hybridCache.Set(memKey, cache, cacheTime(init.view.cache_time, init: init));
                }

                return cache;
            }
            catch { return default; }
        }
        #endregion
    }
}

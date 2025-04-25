using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;
using Shared.Model.Online;
using Shared.Engine;
using Shared.Model.SISI.NextHUB;
using Newtonsoft.Json;
using Shared.PlaywrightCore;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.AspNetCore.Routing;

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

            var init = JsonConvert.DeserializeObject<NxtSettings>($"{{{FileCache.ReadAllText($"NextHUB/{plugin}.json")}}}");
            if (string.IsNullOrEmpty(init.plugin))
                init.plugin = init.displayname;

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

                                    if (Regex.IsMatch(route.Request.Url, init.view.patternFile, RegexOptions.IgnoreCase))
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

                            #region related
                            if (init.view.related)
                            {
                                string content = null;

                                if (init.view.NetworkIdle)
                                {
                                    PlaywrightBase.GotoAsync(page, url);
                                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                                    content = await page.ContentAsync();
                                }
                                else
                                {
                                    var responce = await page.GotoAsync(url, new PageGotoOptions() { WaitUntil = WaitUntilState.DOMContentLoaded });
                                    if (responce != null)
                                        content = await responce.TextAsync();
                                }

                                if (!string.IsNullOrEmpty(content))
                                    cache.recomends = ListController.goPlaylist(host, init.view.contentParse ?? init.contentParse, init, content, plugin);
                            }
                            else
                            {
                                PlaywrightBase.GotoAsync(page, url);
                            }
                            #endregion

                            #region playbtn
                            if (!string.IsNullOrEmpty(init.view.playbtn))
                            {
                                try
                                {
                                    await page.WaitForSelectorAsync(init.view.playbtn, new PageWaitForSelectorOptions
                                    {
                                        Timeout = (float)TimeSpan.FromSeconds(init.view.playbtn_timeout).TotalSeconds
                                    });
                                }
                                catch { }

                                await page.ClickAsync(init.view.playbtn);
                            }
                            #endregion

                            cache.file = await browser.WaitPageResult();
                        }
                    }

                    if (cache.file == null)
                    {
                        proxyManager.Refresh();
                        return default;
                    }

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

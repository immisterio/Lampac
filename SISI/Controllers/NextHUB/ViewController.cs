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

namespace Lampac.Controllers.NextHUB
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("nexthub/vidosik")]
        async public Task<ActionResult> Index(string uri, bool related)
        {
            string plugin = uri.Split("_-:-_")[0];
            string url = uri.Split("_-:-_")[1];

            var init = JsonConvert.DeserializeObject<NxtSettings>($"{{{FileCache.ReadAllText($"NextHUB/{plugin}.json")}}}");

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
                string memKey = $"{init.plugin}:view18:goVideo:{url}";
                if (init.view.bindingToIP)
                    memKey += $":{proxyManager.CurrentProxyIp}";

                if (!hybridCache.TryGetValue(memKey, out (string file, List<HeadersModel> headers, List<PlaylistItem> recomends) cache))
                {
                    using (var browser = new PlaywrightBrowser())
                    {
                        var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy);
                        if (page == null)
                            return default;

                        if (!string.IsNullOrEmpty(init.view.addInitScript))
                            await page.AddInitScriptAsync(init.view.addInitScript);

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

                                if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: init.view.abortMedia, fullCacheJS: init.view.fullCacheJS))
                                    return;

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

                                await route.ContinueAsync();
                            }
                            catch { }
                        });

                        PlaywrightBase.GotoAsync(page, url);

                        if (init.view.related)
                        {
                            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                            string content = await page.ContentAsync();
                            cache.recomends = ListController.goPlaylist(host, init.view.contentParse ?? init.contentParse, init, content, plugin);
                        }

                        if (!string.IsNullOrEmpty(init.view.playbtn))
                        {
                            await page.WaitForSelectorAsync(init.view.playbtn);
                            await page.ClickAsync(init.view.playbtn);
                        }

                        cache.file = await browser.WaitPageResult();
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

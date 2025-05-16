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
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.AspNetCore.Routing;
using HtmlAgilityPack;
using Newtonsoft.Json;

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

            var init = Root.goInit(plugin);
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

            return OnResult(stream_links, init, proxy.proxy, headers_stream: headers);
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
                        if (page == default)
                            return default;

                        string browser_host = "." + Regex.Replace(init.host, "^https?://", "");

                        if (init.view.keepopen)
                            await page.Context.ClearCookiesAsync(new BrowserContextClearCookiesOptions { Domain = browser_host, Name = "cf_clearance" });

                        if (init.cookies != null)
                            await page.Context.AddCookiesAsync(init.cookies);

                        if (!string.IsNullOrEmpty(init.view.addInitScript))
                            await page.AddInitScriptAsync(init.view.addInitScript);

                        #region RouteAsync
                        await page.RouteAsync("**/*", async route =>
                        {
                            try
                            {
                                if (browser.IsCompleted || (init.view.patternAbort != null && Regex.IsMatch(route.Request.Url, init.view.patternAbort, RegexOptions.IgnoreCase)))
                                {
                                    PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                    return;
                                }

                                #region patternFile
                                if (init.view.patternFile != null && Regex.IsMatch(route.Request.Url, init.view.patternFile, RegexOptions.IgnoreCase))
                                {
                                    void setHeaders(Dictionary<string, string> _headers)
                                    {
                                        if (_headers != null && _headers.Count > 0)
                                        {
                                            cache.headers = new List<HeadersModel>(_headers.Count);
                                            foreach (var item in _headers)
                                            {
                                                if (item.Key.ToLower() is "host" or "accept-encoding" or "connection" or "range")
                                                    continue;

                                                cache.headers.Add(new HeadersModel(item.Key, item.Value.ToString()));
                                            }
                                        }
                                    }

                                    setHeaders(route.Request.Headers);

                                    if (init.view.waitLocationFile)
                                    {
                                        await route.ContinueAsync();
                                        string setUri = route.Request.Url;

                                        var response = await page.WaitForResponseAsync(route.Request.Url);
                                        if (response != null && response.Headers.ContainsKey("location"))
                                        {
                                            setHeaders(response.Request.Headers);
                                            setUri = response.Headers["location"];
                                        }

                                        PlaywrightBase.ConsoleLog($"\nPlaywright: SET {setUri}\n{JsonConvert.SerializeObject(cache.headers.ToDictionary(), Formatting.Indented)}\n");
                                        browser.SetPageResult(setUri);
                                    }
                                    else
                                    {
                                        PlaywrightBase.ConsoleLog($"\nPlaywright: SET {route.Request.Url}\n{JsonConvert.SerializeObject(cache.headers.ToDictionary(), Formatting.Indented)}\n");
                                        browser.SetPageResult(route.Request.Url);
                                        await route.AbortAsync();
                                    }

                                    return;
                                }
                                #endregion

                                if (init.view.abortMedia || init.view.fullCacheJS)
                                {
                                    if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: init.view.abortMedia, fullCacheJS: init.view.fullCacheJS))
                                        return;
                                }
                                else
                                {
                                    PlaywrightBase.ConsoleLog($"Playwright: {route.Request.Method} {route.Request.Url}");
                                }

                                await route.ContinueAsync();
                            }
                            catch { }
                        });
                        #endregion

                        #region GotoAsync
                        string html = null;
                        var responce = await page.GotoAsync(init.view.viewsource ? $"view-source:{url}" : url, new PageGotoOptions() { WaitUntil = WaitUntilState.DOMContentLoaded });
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

                                    PlaywrightBase.ConsoleLog("ContentAsync: " + (i + 1));
                                    await Task.Delay(800);
                                }
                            }
                            else
                            {
                                cache.file = goFile(html);
                            }

                            PlaywrightBase.ConsoleLog($"Playwright: SET {cache.file}");
                            #endregion
                        }
                        else if (!string.IsNullOrEmpty(init.view.eval))
                        {
                            #region eval
                            async ValueTask<string> goFile(string _content)
                            {
                                if (!string.IsNullOrEmpty(_content))
                                {
                                    string infile = $"NextHUB/{init.view.eval}";
                                    if (!System.IO.File.Exists(infile))
                                    {
                                        return Root.Eval.Execute<string>(init.view.eval, new { html = _content, plugin, url });
                                    }
                                    else
                                    {
                                        string evaluate = FileCache.ReadAllText(infile);

                                        if (init.view.eval.EndsWith(".js"))
                                            return await page.EvaluateAsync<string>($"(html, plugin, url) => {{ {evaluate} }}", new { _content, plugin, url });

                                        return Root.Eval.Execute<string>(evaluate, new { html = _content, plugin, url });
                                    }
                                }

                                return null;
                            }

                            if (init.view.NetworkIdle)
                            {
                                for (int i = 0; i < 10; i++)
                                {
                                    cache.file = await goFile(await page.ContentAsync());
                                    if (!string.IsNullOrEmpty(cache.file))
                                        break;

                                    PlaywrightBase.ConsoleLog("ContentAsync: " + (i + 1));
                                    await Task.Delay(800);
                                }
                            }
                            else
                            {
                                cache.file = await goFile(html);
                            }

                            PlaywrightBase.ConsoleLog($"Playwright: SET {cache.file}");
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

                    if (string.IsNullOrEmpty(cache.file))
                    {
                        proxyManager.Refresh();
                        return default;
                    }

                    cache.file = cache.file.Replace("\\", "").Replace("&amp;", "&");

                    #region fileEval
                    if (init.view.fileEval != null)
                    {
                        string infile = $"NextHUB/{init.view.fileEval}";
                        if (!System.IO.File.Exists(infile))
                        {
                            cache.file = Root.Eval.Execute<string>(init.view.fileEval, new { cache.file, cache.headers });
                        }
                        else
                        {
                            string evaluate = FileCache.ReadAllText($"NextHUB/{init.view.fileEval}");
                            cache.file = Root.Eval.Execute<string>(evaluate, new { cache.file, cache.headers });
                        }
                    }
                    #endregion

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

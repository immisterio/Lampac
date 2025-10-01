using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Shared.Models.CSharpGlobals;
using Shared.PlaywrightCore;
using System.Net;
using Shared.Models.SISI.NextHUB;

namespace SISI.Controllers.NextHUB
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("nexthub/vidosik")]
        async public ValueTask<ActionResult> Index(string uri, bool related)
        {
            if (!AppInit.conf.sisi.NextHUB)
                return OnError("disabled");

            string plugin = uri.Split("_-:-_")[0];
            string url = uri.Split("_-:-_")[1];

            var init = Root.goInit(plugin)?.Clone();
            if (init == null)
                return OnError("init not found");

            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            if (init.view.initUrlEval != null)
                url = CSharpEval.Execute<string>(init.view.initUrlEval, new NxtUrlRequest(init.host, plugin, url, HttpContext.Request.Query, related));

            return await InvkSemaphore($"nexthub:InvkSemaphore:{url}", async () =>
            {
                (string file, List<HeadersModel> headers, List<PlaylistItem> recomends) video = default;
                if ((init.view.priorityBrowser ?? init.priorityBrowser) == "http" && init.view.viewsource &&
                    (init.view.nodeFile != null || init.view.eval != null || init.view.regexMatch != null) &&
                     init.view.routeEval == null && init.cookies == null && init.view.evalJS == null)
                {
                    reset: video = await goVideoToHttp(rch, plugin, url, init, proxyManager, proxy.proxy);
                    if (string.IsNullOrEmpty(video.file))
                    {
                        if (IsRhubFallback(init))
                            goto reset;

                        return OnError("file", rcache: !rch.enable);
                    }
                }
                else
                {
                    video = await goVideoToBrowser(plugin, url, init, proxyManager, proxy.data);
                    if (string.IsNullOrEmpty(video.file))
                        return OnError("file");
                }

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

                return OnResult(stream_links, init, proxy.proxy, headers_stream: httpHeaders(init.host, init.headers_stream != null ? init.headers_stream : init.headers_stream));
            });
        }


        #region goVideoToBrowser
        async ValueTask<(string file, List<HeadersModel> headers, List<PlaylistItem> recomends)> goVideoToBrowser(string plugin, string url, NxtSettings init, ProxyManager proxyManager, (string ip, string username, string password) proxy)
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
                        var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy, keepopen: init.view.keepopen, deferredDispose: init.view.playbtn != null).ConfigureAwait(false);
                        if (page == default)
                            return default;

                        if (init.cookies != null)
                            await page.Context.AddCookiesAsync(init.cookies).ConfigureAwait(false);

                        if (!string.IsNullOrEmpty(init.view.addInitScript))
                            await page.AddInitScriptAsync(init.view.addInitScript).ConfigureAwait(false);

                        string routeEval = init.view.routeEval;
                        if (!string.IsNullOrEmpty(routeEval) && routeEval.EndsWith(".cs"))
                            routeEval = FileCache.ReadAllText($"NextHUB/sites/{routeEval}");

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

                                #region routeEval
                                if (routeEval != null)
                                {
                                    var options = ScriptOptions.Default
                                        .AddReferences(CSharpEval.ReferenceFromFile("Microsoft.Playwright.dll"))
                                        .AddImports("Microsoft.Playwright");

                                    bool _next = await CSharpEval.ExecuteAsync<bool>(routeEval, new NxtRoute(route, HttpContext.Request.Query, url, null, null, null, null, 0), options);
                                    if (!_next)
                                        return;
                                }
                                #endregion

                                #region patternFile
                                if (init.view.patternFile != null && Regex.IsMatch(route.Request.Url, init.view.patternFile, RegexOptions.IgnoreCase))
                                {
                                    if (init.view.waitForResponse)
                                    {
                                        string result = null;
                                        await browser.ClearContinueAsync(route, page);
                                        var response = await page.WaitForResponseAsync(route.Request.Url);
                                        if (response != null)
                                            result = await response.TextAsync().ConfigureAwait(false);

                                        PlaywrightBase.ConsoleLog($"\nPlaywright: {result}\n");
                                        browser.SetPageResult(result);
                                    }
                                    else
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
                                            await browser.ClearContinueAsync(route, page);
                                            string setUri = route.Request.Url;

                                            var response = await page.WaitForResponseAsync(route.Request.Url);
                                            if (response != null && response.Headers.ContainsKey("location"))
                                            {
                                                setHeaders(response.Request.Headers);
                                                setUri = response.Headers["location"];
                                            }

                                            if (setUri.StartsWith("//"))
                                                setUri = $"{(init.host.StartsWith("https") ? "https" : "http")}:{setUri}";

                                            PlaywrightBase.ConsoleLog($"\nPlaywright: SET {setUri}\n{JsonConvert.SerializeObject(cache.headers.ToDictionary(), Formatting.Indented)}\n");
                                            browser.SetPageResult(setUri);
                                        }
                                        else
                                        {
                                            PlaywrightBase.ConsoleLog($"\nPlaywright: SET {route.Request.Url}\n{JsonConvert.SerializeObject(cache.headers.ToDictionary(), Formatting.Indented)}\n");
                                            browser.SetPageResult(route.Request.Url);
                                            await route.AbortAsync();
                                        }
                                    }

                                    return;
                                }
                                #endregion

                                #region patternAbortEnd
                                if (init.view.patternAbortEnd != null && Regex.IsMatch(route.Request.Url, init.view.patternAbortEnd, RegexOptions.IgnoreCase))
                                {
                                    PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                    return;
                                }
                                #endregion

                                #region patternWhiteRequest
                                if (init.view.patternWhiteRequest != null && route.Request.Url != url && !Regex.IsMatch(route.Request.Url, init.view.patternWhiteRequest, RegexOptions.IgnoreCase))
                                {
                                    PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                    return;
                                }
                                #endregion

                                #region abortMedia
                                if (init.view.abortMedia || init.view.fullCacheJS)
                                {
                                    if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: init.view.abortMedia, fullCacheJS: init.view.fullCacheJS))
                                        return;
                                }
                                else
                                {
                                    PlaywrightBase.ConsoleLog($"Playwright: {route.Request.Method} {route.Request.Url}");
                                }
                                #endregion

                                await browser.ClearContinueAsync(route, page);
                            }
                            catch { }
                        });
                        #endregion

                        #region GotoAsync
                        resetGotoAsync: string html = null;
                        var responce = await page.GotoAsync(init.view.viewsource ? $"view-source:{url}" : url, new PageGotoOptions() 
                        {
                            Timeout = 10_000,
                            WaitUntil = WaitUntilState.DOMContentLoaded 
                        }).ConfigureAwait(false);

                        if (responce != null)
                            html = await responce.TextAsync().ConfigureAwait(false);
                        #endregion

                        if (init.view.waitForResponse)
                            html = await browser.WaitPageResult().ConfigureAwait(false);

                        #region WaitForSelector
                        if (!string.IsNullOrEmpty(init.view.waitForSelector) || !string.IsNullOrEmpty(init.view.playbtn))
                        {
                            try
                            {
                                await page.WaitForSelectorAsync(init.view.waitForSelector ?? init.view.playbtn, new PageWaitForSelectorOptions
                                {
                                    Timeout = init.view.waitForSelector_timeout

                                }).ConfigureAwait(false);
                            }
                            catch { }

                            html = await page.ContentAsync().ConfigureAwait(false);
                        }
                        #endregion

                        #region iframe
                        if (init.view.iframe != null && url.Contains(init.host))
                        {
                            string iframeUrl = CSharpEval.Execute<string>(evalCodeToRegexMatch(init.view.iframe), new NxtRegexMatch(html, init.view.iframe));
                            if (!string.IsNullOrEmpty(iframeUrl) && iframeUrl != url)
                            {
                                url = init.view.iframe.format != null ? init.view.iframe.format.Replace("{value}", iframeUrl) : iframeUrl;
                                goto resetGotoAsync;
                            }
                        }
                        #endregion

                        if (!string.IsNullOrEmpty(init.view.playbtn))
                            await page.ClickAsync(init.view.playbtn).ConfigureAwait(false);

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
                                    cache.file = goFile(await page.ContentAsync().ConfigureAwait(false));
                                    if (!string.IsNullOrEmpty(cache.file))
                                        break;

                                    PlaywrightBase.ConsoleLog("ContentAsync: " + (i + 1));
                                    await Task.Delay(800).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                cache.file = goFile(html);
                            }

                            PlaywrightBase.ConsoleLog($"Playwright: SET {cache.file}");
                            #endregion
                        }
                        else if (init.view.regexMatch != null)
                        {
                            #region regexMatch
                            if (init.view.NetworkIdle)
                            {
                                for (int i = 0; i < 10; i++)
                                {
                                    cache.file = CSharpEval.Execute<string>(evalCodeToRegexMatch(init.view.regexMatch), new NxtRegexMatch(html, init.view.regexMatch));
                                    if (!string.IsNullOrEmpty(cache.file) && init.view.regexMatch.format != null)
                                        cache.file = init.view.regexMatch.format.Replace("{value}", cache.file).Replace("{host}", init.host);

                                    if (!string.IsNullOrEmpty(cache.file))
                                        break;

                                    PlaywrightBase.ConsoleLog("ContentAsync: " + (i + 1));
                                    await Task.Delay(800).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                cache.file = CSharpEval.Execute<string>(evalCodeToRegexMatch(init.view.regexMatch), new NxtRegexMatch(html, init.view.regexMatch));
                                if (!string.IsNullOrEmpty(cache.file) && init.view.regexMatch.format != null)
                                    cache.file = init.view.regexMatch.format.Replace("{value}", cache.file).Replace("{host}", init.host);
                            }

                            PlaywrightBase.ConsoleLog($"Playwright: SET {cache.file}");
                            #endregion
                        }
                        else if (string.IsNullOrEmpty(init.view.eval ?? init.view.evalJS))
                        {
                            cache.file = await browser.WaitPageResult().ConfigureAwait(false);
                        }

                        cache.file = cache.file?.Replace("\\", "")?.Replace("&amp;", "&");

                        #region eval
                        if (!string.IsNullOrEmpty(init.view.eval ?? init.view.evalJS))
                        {
                            Task<string> goFile(string _content)
                            {
                                if (!string.IsNullOrEmpty(_content))
                                {
                                    string infile = $"NextHUB/sites/{init.view.eval ?? init.view.evalJS}";
                                    if ((infile.EndsWith(".cs") || infile.EndsWith(".js")) && System.IO.File.Exists(infile))
                                    {
                                        string evaluate = FileCache.ReadAllText(infile);

                                        if (infile.EndsWith(".js"))
                                            return page.EvaluateAsync<string>($"(html, plugin, url, file) => {{ {evaluate} }}", new { _content, plugin, url, cache.file });

                                        var nxt = new NxtEvalView(init, HttpContext.Request.Query, _content, plugin, url, cache.file, cache.headers, proxyManager);
                                        return CSharpEval.ExecuteAsync<string>(goEval(evaluate), nxt, Root.evalOptionsFull);
                                    }
                                    else
                                    {
                                        if (init.view.evalJS != null)
                                            return page.EvaluateAsync<string>($"(html, plugin, url, file) => {{ {init.view.evalJS} }}", new { _content, plugin, url, cache.file });

                                        var nxt = new NxtEvalView(init, HttpContext.Request.Query, _content, plugin, url, cache.file, cache.headers, proxyManager);
                                        return CSharpEval.ExecuteAsync<string>(goEval(init.view.eval), nxt, Root.evalOptionsFull);
                                    }
                                }

                                return null;
                            }

                            if (init.view.NetworkIdle)
                            {
                                for (int i = 0; i < 10; i++)
                                {
                                    cache.file = await goFile(await page.ContentAsync().ConfigureAwait(false)).ConfigureAwait(false);
                                    if (!string.IsNullOrEmpty(cache.file))
                                        break;

                                    PlaywrightBase.ConsoleLog("ContentAsync: " + (i + 1));
                                    await Task.Delay(800).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                cache.file = await goFile(html).ConfigureAwait(false);
                            }

                            PlaywrightBase.ConsoleLog($"Playwright: SET {cache.file}");
                        }
                        #endregion

                        if (string.IsNullOrEmpty(cache.file))
                        {
                            proxyManager.Refresh();
                            return default;
                        }

                        if (cache.file.StartsWith("GotoAsync:"))
                        {
                            url = cache.file.Replace("GotoAsync:", "").Trim();
                            goto resetGotoAsync;
                        }

                        #region related
                        if (init.view.related && cache.recomends == null)
                        {
                            if (init.view.NetworkIdle)
                            {
                                string contetnt = await page.ContentAsync().ConfigureAwait(false);
                                cache.recomends = ListController.goPlaylist(requestInfo, host, init.view.relatedParse ?? init.contentParse, init, contetnt, plugin);
                            }
                            else
                            {
                                cache.recomends = ListController.goPlaylist(requestInfo, host, init.view.relatedParse ?? init.contentParse, init, html, plugin);
                            }
                        }
                        #endregion
                    }

                    proxyManager.Success();
                    hybridCache.Set(memKey, cache, cacheTime(init.view.cache_time, init: init));
                }

                return cache;
            }
            catch (Exception ex)
            {
                if (init.debug)
                    Console.WriteLine(ex);

                return default; 
            }
        }
        #endregion

        #region goVideoToHttp
        async ValueTask<(string file, List<HeadersModel> headers, List<PlaylistItem> recomends)> goVideoToHttp(RchClient rch, string plugin, string url, NxtSettings init, ProxyManager proxyManager, WebProxy proxy)
        {
            if (string.IsNullOrEmpty(url))
                return default;

            try
            {
                string memKey = $"nexthub:view18:goVideo:{url}";

                if (init.view.bindingToIP)
                    memKey = rch.ipkey(memKey, proxyManager);

                if (!hybridCache.TryGetValue(memKey, out (string file, List<HeadersModel> headers, List<PlaylistItem> recomends) cache))
                {
                    resetGotoAsync:
                    string html = rch.enable ? await rch.Get(url, httpHeaders(init)) :
                                               await Http.Get(url, headers: httpHeaders(init), proxy: proxy, timeoutSeconds: 8);

                    if (string.IsNullOrEmpty(html))
                        return default;

                    #region iframe
                    if (init.view.iframe != null && url.Contains(init.host))
                    {
                        string iframeUrl = CSharpEval.Execute<string>(evalCodeToRegexMatch(init.view.iframe), new NxtRegexMatch(html, init.view.iframe));
                        if (!string.IsNullOrEmpty(iframeUrl) && iframeUrl != url)
                        {
                            url = init.view.iframe.format != null ? init.view.iframe.format.Replace("{value}", iframeUrl) : iframeUrl;
                            goto resetGotoAsync;
                        }
                    }
                    #endregion

                    if (init.view.nodeFile != null)
                    {
                        #region nodeFile
                        string goFile(string _content)
                        {
                            var doc = new HtmlDocument();
                            doc.LoadHtml(_content);
                            var videoNode = doc.DocumentNode.SelectSingleNode(init.view.nodeFile.node);
                            if (videoNode != null)
                                return (!string.IsNullOrEmpty(init.view.nodeFile.attribute) ? videoNode.GetAttributeValue(init.view.nodeFile.attribute, null) : videoNode.InnerText)?.Trim();

                            return null;
                        }

                        cache.file = goFile(html);

                        PlaywrightBase.ConsoleLog($"Playwright: SET {cache.file}");
                        #endregion
                    }
                    else if (init.view.regexMatch != null)
                    {
                        #region regexMatch
                        cache.file = CSharpEval.Execute<string>(evalCodeToRegexMatch(init.view.regexMatch), new NxtRegexMatch(html, init.view.regexMatch));
                        if (!string.IsNullOrEmpty(cache.file) && init.view.regexMatch.format != null)
                            cache.file = init.view.regexMatch.format.Replace("{value}", cache.file).Replace("{host}", init.host);

                        PlaywrightBase.ConsoleLog($"Playwright: SET {cache.file}");
                        #endregion
                    }

                    cache.file = cache.file?.Replace("\\", "")?.Replace("&amp;", "&");

                    #region eval
                    if (!string.IsNullOrEmpty(init.view.eval))
                    {
                        var nxt = new NxtEvalView(init, HttpContext.Request.Query, html, plugin, url, cache.file, cache.headers, proxyManager);
                        cache.file = await CSharpEval.ExecuteAsync<string>(goEval(init.view.eval), nxt, Root.evalOptionsFull).ConfigureAwait(false);

                        PlaywrightBase.ConsoleLog($"Playwright: SET {cache.file}");
                    }
                    #endregion

                    if (string.IsNullOrEmpty(cache.file))
                    {
                        if (!rch.enable)
                            proxyManager.Refresh();

                        return default;
                    }

                    if (cache.file.StartsWith("GotoAsync:"))
                    {
                        url = cache.file.Replace("GotoAsync:", "").Trim();
                        goto resetGotoAsync;
                    }

                    if (init.view.related && cache.recomends == null)
                        cache.recomends = ListController.goPlaylist(requestInfo, host, init.view.relatedParse ?? init.contentParse, init, html, plugin);

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(memKey, cache, cacheTime(init.view.cache_time, init: init));
                }

                return cache;
            }
            catch (Exception ex)
            {
                if (init.debug)
                    Console.WriteLine(ex);

                return default;
            }
        }
        #endregion


        #region evalCodeToRegexMatch
        static string evalCodeToRegexMatch(RegexMatchSettings rm)
        {
            return @"if (m.matches != null && m.matches.Length > 0)
            {
                foreach (string q in m.matches)
                {
                    string file = Regex.Match(html, m.pattern.Replace(""{value}"", $""{q}""), RegexOptions.IgnoreCase).Groups[m.index].Value;
                    if (!string.IsNullOrEmpty(file))
                        return file;
                }
                return null;
            }

            return Regex.Match(html, m.pattern, RegexOptions.IgnoreCase).Groups[m.index].Value;";
        }
        #endregion

        #region goEval
        static string goEval(string evalcode)
        {
            string infile = $"NextHUB/sites/{evalcode}";
            if (infile.EndsWith(".cs") && System.IO.File.Exists(infile))
                evalcode = FileCache.ReadAllText(infile);

            if (evalcode.Contains("{include:"))
            {
                string includePattern = @"{include:(?<file>[^}]+)}";
                var matches = Regex.Matches(evalcode, includePattern);
                foreach (Match match in matches)
                {
                    string file = match.Groups["file"].Value.Trim();
                    if (System.IO.File.Exists($"NextHUB/utils/{file}"))
                    {
                        string includeCode = FileCache.ReadAllText($"NextHUB/utils/{file}");
                        evalcode = evalcode.Replace(match.Value, includeCode);
                    }
                }
            }

            return evalcode;
        }
        #endregion
    }
}

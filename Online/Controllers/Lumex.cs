﻿using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Online;
using Shared.Engine;
using Shared.Engine.CORE;
using Shared.Engine.Online;
using Shared.Model.Online;
using Shared.Model.Online.Lumex;
using Shared.Model.Templates;
using Shared.PlaywrightCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Controllers.LITE
{
    public class Lumex : BaseOnlineController
    {
        public static List<DatumDB> database = null;

        [HttpGet]
        [Route("lite/lumex")]
        async public ValueTask<ActionResult> Index(long content_id, string content_type, string imdb_id, long kinopoisk_id, string title, string original_title, string t, int clarification, int s = -1, int serial = -1, bool origsource = false, bool rjson = false, bool similar = false)
        {
            var init = await loadKit(AppInit.conf.Lumex);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (init.priorityBrowser == "firefox")
            {
                if (Firefox.Status == PlaywrightStatus.disabled)
                    return OnError();
            }
            else
            {
                if (Chromium.Status == PlaywrightStatus.disabled)
                    return OnError();
            }

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            var oninvk = new LumexInvoke
            (
               init,
               (url, referer) => HttpClient.Get(init.cors(url), referer: referer, timeoutSeconds: 8, proxy: proxy.proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy.proxy),
               host,
               requesterror: () => proxyManager.Refresh()
            );

            if (similar || (content_id == 0 && kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id)))
            {
                string memKey = $"lumex:search:{title}:{original_title}:{clarification}";
                if (!hybridCache.TryGetValue(memKey, out SimilarTpl search))
                {
                    if (string.IsNullOrEmpty(init.token) && database == null && init.spider)
                        database = JsonHelper.ListReader<DatumDB>("data/lumex.json", 105000);

                    search = await oninvk.Search(title, original_title, serial, clarification, database);
                    if (search.data?.Count == 0)
                        return OnError("search");

                    hybridCache.Set(memKey, search, cacheTime(40, init: init));
                }

                return ContentTo(rjson ? search.ToJson() : search.ToHtml());
            }

            var cache = await InvokeCache<EmbedModel>($"videocdn:{content_id}:{content_type}:{kinopoisk_id}:{imdb_id}:{proxyManager.CurrentProxyIp}", cacheTime(10, init: init), proxyManager,  async res =>
            {
                string content_uri = null;
                var content_headers = new List<HeadersModel>();

                #region uri
                string targetUrl = $"https://p.{init.iframehost}/{init.clientId}";
                if (content_id > 0)
                {
                    targetUrl += $"/{content_type}/{content_id}";
                }
                else
                {
                    if (kinopoisk_id > 0)
                        targetUrl += $"?kp_id={kinopoisk_id}";
                    if (!string.IsNullOrEmpty(imdb_id))
                        targetUrl += (targetUrl.Contains("?") ? "&" : "?") + $"imdb_id={imdb_id}";
                }
                #endregion

                if (init.priorityBrowser == "http" && kinopoisk_id > 0)
                {
                    content_uri = $"https://api.{init.iframehost}/content?clientId={init.clientId}&contentType=short&kpId={kinopoisk_id}";
                    content_headers = HeadersModel.Init(Chromium.baseContextOptions.ExtraHTTPHeaders);
                    content_headers.Add(new HeadersModel("accept", "*/*"));
                    content_headers.Add(new HeadersModel("origin", $"https://p.{init.iframehost}"));
                    content_headers.Add(new HeadersModel("referer", $"https://p.{init.iframehost}/"));
                    content_headers.Add(new HeadersModel("sec-fetch-site", "same-site "));
                    content_headers.Add(new HeadersModel("sec-fetch-mode", "cors"));
                    content_headers.Add(new HeadersModel("sec-fetch-dest", "empty"));
                }
                else if (init.priorityBrowser == "scraping")
                {
                    #region Scraping
                    using (var browser = new Scraping(targetUrl, "/content\\?contentId=", null))
                    {
                        browser.OnRequest += e =>
                        {
                            if (Regex.IsMatch(e.HttpClient.Request.Url, "\\.(css|woff2|jpe?g|png|ico)") ||
                               !Regex.IsMatch(e.HttpClient.Request.Url, "(lumex|cloudflare|sentry|gstatic)\\."))
                            {
                                e.Ok(string.Empty);
                            }
                        };

                        var scrap = await browser.WaitPageResult(15);

                        if (scrap != null)
                        {
                            content_uri = scrap.Url;
                            foreach (var item in scrap.Headers)
                            {
                                if (item.Name.ToLower() is "host" or "accept-encoding" or "connection" or "range" or "cookie")
                                    continue;

                                content_headers.Add(new HeadersModel(item.Name, item.Value));
                            }
                        }
                    }
                    #endregion
                }
                else
                {
                    #region Playwright
                    try
                    {
                        using (var browser = new PlaywrightBrowser())
                        {
                            var page = await browser.NewPageAsync(init.plugin, proxy: proxy.data).ConfigureAwait(false);
                            if (page == null)
                                return null;

                            await page.Context.ClearCookiesAsync(new BrowserContextClearCookiesOptions { Domain = $"api.{init.iframehost}", Name = "x-csrf-token" });

                            await page.RouteAsync("**/*", async route =>
                            {
                                try
                                {
                                    if (content_uri != null || browser.IsCompleted)
                                    {
                                        PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                        await route.AbortAsync();
                                        return;
                                    }

                                    if (route.Request.Url.Contains("/content?clientId="))
                                    {
                                        content_uri = route.Request.Url.Replace("%3D", "=").Replace("%3F", "&");
                                        foreach (var item in route.Request.Headers)
                                        {
                                            if (item.Key is "host" or "accept-encoding" or "connection" or "range" or "cookie")
                                                continue;

                                            content_headers.Add(new HeadersModel(item.Key, item.Value));
                                        }

                                        foreach (var h in new List<(string key, string val)>
                                        {
                                            ("sec-fetch-site", "same-site"),
                                            ("sec-fetch-mode", "cors"),
                                            ("sec-fetch-dest", "empty"),
                                        })
                                        {
                                            if (!route.Request.Headers.ContainsKey(h.key))
                                                content_headers.Add(new HeadersModel(h.key, h.val));
                                        }

                                        browser.SetPageResult(string.Empty);
                                        await route.AbortAsync();
                                        return;
                                    }

                                    if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                        return;

                                    await route.ContinueAsync();
                                }
                                catch { }
                            });

                            PlaywrightBase.GotoAsync(page, targetUrl);
                            await browser.WaitPageResult().ConfigureAwait(false);
                        }
                    }
                    catch { }
                    #endregion
                }

                if (content_uri == null)
                    return res.Fail("content_uri");

                var result = await HttpClient.BaseGetAsync(content_uri, timeoutSeconds: 8, proxy: proxy.proxy, headers: content_headers);

                if (string.IsNullOrEmpty(result.content))
                {
                    proxyManager.Refresh();
                    return res.Fail("content");
                }

                if (!result.response.Headers.TryGetValues("Set-Cookie", out var cook))
                {
                    proxyManager.Refresh();
                    return res.Fail("cook");
                }

                string csrf = Regex.Match(cook.FirstOrDefault() ?? "", "x-csrf-token=([^\n\r; ]+)").Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(csrf))
                {
                    proxyManager.Refresh();
                    return res.Fail("csrf");
                }

                content_headers.Add(new HeadersModel("x-csrf-token", csrf.Split("%")[0]));
                content_headers.Add(new HeadersModel("cookie", $"x-csrf-token={csrf}"));

                var md = JsonConvert.DeserializeObject<JObject>(result.content)["player"].ToObject<EmbedModel>();
                md.csrf = CrypTo.md5(DateTime.Now.ToFileTime().ToString());

                hybridCache.Set(md.csrf, content_headers, DateTime.Now.AddDays(1));

                return md;
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, accsArgs(string.Empty), content_id, content_type, imdb_id, kinopoisk_id, title, original_title, clarification, t, s, rjson: rjson), origsource: origsource);
        }


        #region Video
        [HttpGet]
        [Route("lite/lumex/video")]
        [Route("lite/lumex/video.m3u8")]
        async public ValueTask<ActionResult> Video(string playlist, string csrf, int max_quality)
        {
            var init = await loadKit(AppInit.conf.Lumex);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(playlist) || string.IsNullOrEmpty(csrf))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            string memkey = $"lumex/video:{playlist}:{csrf}";
            if (!hybridCache.TryGetValue(memkey, out string hls))
            {
                if (!hybridCache.TryGetValue(csrf, out List<HeadersModel> content_headers))
                    return OnError();

                var result = await HttpClient.Post<JObject>($"https://api.{init.iframehost}" + playlist, "", httpversion: 2, proxy: proxy, timeoutSeconds: 8, headers: content_headers);

                if (result == null || !result.ContainsKey("url"))
                    return OnError();

                string url = result.Value<string>("url");
                if (string.IsNullOrEmpty(url))
                    return OnError();

                if (url.StartsWith("/"))
                    hls = $"{init.scheme}:{url}";
                else
                    hls = url;

                hybridCache.Set(memkey, hls, cacheTime(20, init: init));
            }

            string sproxy(string uri) => HostStreamProxy(init, uri, proxy: proxy);

            if (max_quality > 0 && !init.hls)
            {
                var streams = new List<(string link, string quality)>(5);
                foreach (int q in new int[] { 1080, 720, 480, 360, 240 })
                {
                    if (max_quality >= q)
                        streams.Add((sproxy(Regex.Replace(hls, "/hls\\.m3u8$", $"/{q}.mp4")), $"{q}p"));
                }

                return ContentTo(VideoTpl.ToJson("play", streams[0].link, streams[0].quality, streamquality: new StreamQualityTpl(streams), vast: init.vast));
            }

            return Redirect(sproxy(hls));
        }
        #endregion
    }
}

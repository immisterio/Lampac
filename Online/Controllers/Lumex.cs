using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Lumex;
using Shared.Models.Online.Settings;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class Lumex : BaseOnlineController<LumexSettings>
    {
        #region EventListener.ProxyApiCreateHttpRequest
        //static Lumex()
        //{
        //    FixHostEvent();
        //}

        //static Dictionary<string, string> ips = null;

        //public static void FixHostEvent()
        //{
        //    if (ips != null) 
        //        return;

        //    ips = new Dictionary<string, string>();

        //    EventListener.ProxyApiCreateHttpRequest += async httpRequestModel =>
        //    {
        //        if (!httpRequestModel.uri.Host.Contains("mediaaly.pro"))
        //            return;

        //        string targetHost = httpRequestModel.uri.Host.Replace("mediaaly.pro", "saicdn.com");

        //        if (!ips.TryGetValue(targetHost, out string dns_ip))
        //        {
        //            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        //            {
        //                var lookup = new LookupClient();
        //                var queryType = await lookup.QueryAsync(targetHost, QueryType.A, cancellationToken: cts.Token);

        //                dns_ip = queryType?.Answers?.ARecords()?.FirstOrDefault()?.Address?.ToString();

        //                if (string.IsNullOrEmpty(dns_ip))
        //                    return;

        //                ips.TryAdd(targetHost, dns_ip);
        //            }
        //        }

        //        var newUri = new Uri(httpRequestModel.requestMessage.RequestUri.AbsoluteUri.Replace(httpRequestModel.uri.Host, dns_ip));
        //        httpRequestModel.requestMessage.RequestUri = newUri;
        //    };
        //}
        #endregion

        #region database
        static List<DatumDB> databaseCache;

        public static IEnumerable<DatumDB> database
        {
            get
            {
                if (AppInit.conf.multiaccess)
                    return databaseCache ??= JsonHelper.ListReader<DatumDB>("data/lumex.json", 130_000);

                return JsonHelper.IEnumerableReader<DatumDB>("data/lumex.json");
            }
        }
        #endregion

        public Lumex() : base(AppInit.conf.Lumex) { }

        [HttpGet]
        [Route("lite/lumex")]
        async public Task<ActionResult> Index(long content_id, string content_type, string imdb_id, long kinopoisk_id, string title, string original_title, string t, int clarification, int s = -1, int serial = -1, bool rjson = false, bool similar = false)
        {
            if (await IsRequestBlocked(rch: false))
                return badInitMsg;

            if (init.priorityBrowser == "firefox")
            {
                if (Firefox.Status == PlaywrightStatus.disabled)
                    return OnError("Firefox disabled");
            }
            else if (init.priorityBrowser != "http")
            {
                if (Chromium.Status == PlaywrightStatus.disabled)
                    return OnError("Chromium disabled");
            }

            var oninvk = new LumexInvoke
            (
               host,
               init,
               httpHydra,
               streamfile => HostStreamProxy(streamfile),
               requesterror: () => proxyManager?.Refresh()
            );

            if (similar || (content_id == 0 && kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id)))
            {
                return await InvkSemaphore($"lumex:search:{title}:{original_title}:{clarification}", async key =>
                {
                    if (!hybridCache.TryGetValue(key, out SimilarTpl search))
                    {
                        search = await oninvk.Search(title, original_title, serial, clarification, database);
                        if (search == null || search.IsEmpty)
                            return OnError("search");

                        hybridCache.Set(key, search, cacheTime(40));
                    }

                    return await ContentTpl(search);
                });
            }

            var cache = await InvokeCacheResult<EmbedModel>($"videocdn:{content_id}:{content_type}:{kinopoisk_id}:{imdb_id}:{proxyManager?.CurrentProxyIp}", 10, async e =>
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

                    content_headers = HeadersModel.Init(new Dictionary<string, string>(Http.defaultUaHeaders)
                    {
                        ["accept"] = "*/*",
                        ["accept-language"] = "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5",
                        ["origin"] = $"https://p.{init.iframehost}",
                        ["referer"] = $"https://p.{init.iframehost}/",
                        ["sec-fetch-site"] = "same-site",
                        ["sec-fetch-mode"] = "cors",
                        ["sec-fetch-dest"] = "empty"
                    });
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
                        using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                        {
                            var page = await browser.NewPageAsync(init.plugin, proxy: proxy_data).ConfigureAwait(false);
                            if (page == null)
                                return null;

                            await page.Context.ClearCookiesAsync(new BrowserContextClearCookiesOptions { Domain = $"api.{init.iframehost}", Name = "x-csrf-token" });

                            await page.RouteAsync("**/*", async route =>
                            {
                                try
                                {
                                    if (content_uri != null || browser.IsCompleted)
                                    {
                                        PlaywrightBase.ConsoleLog(() => $"Playwright: Abort {route.Request.Url}");
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
                    return e.Fail("content_uri", refresh_proxy: true);

                var result = await Http.BaseGet(content_uri, proxy: proxy, headers: content_headers);

                if (string.IsNullOrEmpty(result.content))
                    return e.Fail("content", refresh_proxy: true);

                if (!result.response.Headers.TryGetValues("Set-Cookie", out var cook))
                    return e.Fail("cook", refresh_proxy: true);

                string csrf = Regex.Match(cook.FirstOrDefault() ?? "", "x-csrf-token=([^\n\r; ]+)").Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(csrf))
                    return e.Fail("csrf", refresh_proxy: true);

                content_headers.Add(new HeadersModel("x-csrf-token", csrf.Split("%")[0]));
                content_headers.Add(new HeadersModel("cookie", $"x-csrf-token={csrf}"));

                var md = JsonConvert.DeserializeObject<JObject>(result.content)["player"].ToObject<EmbedModel>();
                md.csrf = CrypTo.md5(DateTime.Now.ToFileTime().ToString());

                hybridCache.Set(md.csrf, content_headers, DateTime.Now.AddDays(1));

                return e.Success(md);
            });

            return await ContentTpl(cache, 
                () => oninvk.Tpl(cache.Value, accsArgs(string.Empty), content_id, content_type, imdb_id, kinopoisk_id, title, original_title, clarification, t, s, rjson: rjson)
            );
        }

        #region Video
        [HttpGet]
        [Route("lite/lumex/video")]
        [Route("lite/lumex/video.m3u8")]
        async public ValueTask<ActionResult> Video(string playlist, string csrf, int max_quality)
        {
            if (string.IsNullOrEmpty(playlist) || string.IsNullOrEmpty(csrf))
                return OnError();

            if (await IsRequestBlocked(rch: false, rch_check: false))
                return badInitMsg;

            return await InvkSemaphore($"lumex/video:{playlist}:{csrf}", async key =>
            {
                if (!hybridCache.TryGetValue(key, out string hls))
                {
                    if (!hybridCache.TryGetValue(csrf, out List<HeadersModel> content_headers))
                        return OnError();

                    var result = await Http.Post<JObject>($"https://api.{init.iframehost}" + playlist, "", httpversion: 2, proxy: proxy, timeoutSeconds: 8, headers: content_headers);

                    if (result == null || !result.ContainsKey("url"))
                        return OnError(refresh_proxy: true);

                    string url = result.Value<string>("url");
                    if (string.IsNullOrEmpty(url))
                        return OnError();

                    if (url.StartsWith("/"))
                        hls = $"{init.scheme}:{url}";
                    else
                        hls = url;

                    hybridCache.Set(key, hls, cacheTime(20));
                }

                if (max_quality > 0 && !init.hls)
                {
                    var streamquality = new StreamQualityTpl();

                    foreach (int q in new int[] { 1080, 720, 480, 360, 240 })
                    {
                        if (max_quality >= q)
                            streamquality.Append(HostStreamProxy(Regex.Replace(hls, "/hls\\.m3u8$", $"/{q}.mp4")), $"{q}p");
                    }

                    if (!streamquality.Any())
                        return OnError("streams");

                    var first = streamquality.Firts();
                    return ContentTo(VideoTpl.ToJson("play", first.link, first.quality, streamquality: streamquality, vast: init.vast));
                }

                return Redirect(HostStreamProxy(hls));
            });
        }
        #endregion
    }
}

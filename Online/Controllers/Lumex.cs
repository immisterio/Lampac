using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Model.Online.Lumex;
using Shared.Model.Online;
using System.Linq;
using System.Collections.Generic;
using System;
using Shared.Engine;

namespace Lampac.Controllers.LITE
{
    public class Lumex : BaseOnlineController
    {
        static List<DatumDB> database = null;

        [HttpGet]
        [Route("lite/lumex")]
        async public Task<ActionResult> Index(long content_id, string content_type, string imdb_id, long kinopoisk_id, string title, string original_title, string t, int clarification, int s = -1, int serial = -1, bool origsource = false, bool rjson = false, bool similar = false)
        {
            var init = await loadKit(AppInit.conf.Lumex);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

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
                var search = await InvokeCache<SimilarTpl>($"lumex:search:{title}:{original_title}:{clarification}", cacheTime(40, init: init), async res =>
                {
                    if (string.IsNullOrEmpty(init.token) && database == null && init.spider)
                        database = JsonHelper.ListReader<DatumDB>("data/lumex.json", 105000);

                    return await oninvk.Search(title, original_title, serial, clarification, database);
                });

                return OnResult(search, () => rjson ? search.Value.ToJson() : search.Value.ToHtml());
            }

            var cache = await InvokeCache<EmbedModel>($"videocdn:{content_id}:{content_type}:{kinopoisk_id}:{imdb_id}:{proxyManager.CurrentProxyIp}", cacheTime(10, init: init), proxyManager,  async res =>
            {
                string content_uri = null;
                var content_headers = new List<HeadersModel>();

                #region Firefox
                try
                {
                    using(var browser = new Firefox())
                    {
                        var page = await browser.NewPageAsync(init.plugin, proxy: proxy.data);
                        if (page == null)
                            return null;

                        await page.RouteAsync("**/*", async route =>
                        {
                            if (content_uri != null || browser.IsCompleted)
                            {
                                Console.WriteLine($"Playwright: Abort {route.Request.Url}");
                                await route.AbortAsync();
                                return;
                            }

                            if (route.Request.Url.Contains("/content?clientId="))
                            {
                                content_uri = route.Request.Url.Replace("%3D", "=").Replace("%3F", "&");
                                foreach (var item in route.Request.Headers)
                                {
                                    if (item.Key == "host" || item.Key == "accept-encoding" || item.Key == "connection" || item.Key == "range")
                                        continue;

                                    content_headers.Add(new HeadersModel(item.Key, item.Value));
                                }

                                browser.IsCompleted = true;
                                browser.completionSource.SetResult(string.Empty);
                                await route.AbortAsync();
                                return;
                            }

                            if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                return;

                            await route.ContinueAsync();
                        });

                        string uri = $"https://p.{init.iframehost}/{init.clientId}";
                        if (content_id > 0)
                        {
                            uri += $"/{content_type}/{content_id}";
                        }
                        else
                        {
                            if (kinopoisk_id > 0)
                                uri += $"?kp_id={kinopoisk_id}";
                            if (!string.IsNullOrEmpty(imdb_id))
                                uri += (uri.Contains("?") ? "&" : "?") + $"imdb_id={imdb_id}";
                        }

                        PlaywrightBase.GotoAsync(page, uri);
                        await browser.WaitPageResult();
                    }
                }
                catch { }
                #endregion

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
                var hcookie = content_headers.FirstOrDefault(i => i.name == "cookie");
                if (hcookie != null)
                    hcookie.val = $"x-csrf-token={csrf}; {hcookie.val}";

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
        async public Task<ActionResult> Video(string playlist, string csrf, int max_quality)
        {
            var init = await loadKit(AppInit.conf.Lumex);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (Firefox.Status == PlaywrightStatus.disabled)
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

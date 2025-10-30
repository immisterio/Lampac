using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class Videoseed : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/videoseed")]
        async public ValueTask<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int year, int s = -1, bool rjson = false, int serial = -1)
        {
            var init = await loadKit(AppInit.conf.Videoseed);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.token))
                return OnError();

            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string memKey = $"videoseed:view:{kinopoisk_id}:{imdb_id}:{original_title}";

            return await InvkSemaphore(init, memKey, async () =>
            {
                #region search
                if (!hybridCache.TryGetValue(memKey, out (Dictionary<string, JObject> seasons, string iframe) cache))
                {
                    #region goSearch
                    async ValueTask<JToken> goSearch(bool isOk, string arg)
                    {
                        if (!isOk)
                            return null;

                        string uri = $"{init.host}/apiv2.php?item={(serial == 1 ? "serial" : "movie")}&token={init.token}" + arg;
                        var root = await Http.Get<JObject>(uri, timeoutSeconds: 8, headers: httpHeaders(init), proxy: proxy.proxy);

                        if (root == null || !root.ContainsKey("data") || root.Value<string>("status") == "error")
                        {
                            proxyManager.Refresh();
                            return null;
                        }

                        return root["data"]?.First;
                    }
                    #endregion

                    var data = await goSearch(kinopoisk_id > 0, $"&kp={kinopoisk_id}") ??
                               await goSearch(!string.IsNullOrEmpty(imdb_id), $"&tmdb={imdb_id}") ??
                               await goSearch(!string.IsNullOrEmpty(original_title), $"&q={HttpUtility.UrlEncode(original_title)}&release_year_from={year - 1}&release_year_to={year + 1}");

                    if (data == null)
                    {
                        proxyManager.Refresh();
                        return OnError();
                    }

                    if (serial == 1)
                        cache.seasons = data?["seasons"]?.ToObject<Dictionary<string, JObject>>();
                    else
                        cache.iframe = data?.Value<string>("iframe");

                    if (cache.seasons == null && string.IsNullOrEmpty(cache.iframe))
                    {
                        proxyManager.Refresh();
                        return OnError();
                    }

                    proxyManager.Success();
                    hybridCache.Set(memKey, cache, cacheTime(40, init: init));
                }
                #endregion

                if (cache.iframe != null)
                {
                    #region Фильм
                    var mtpl = new MovieTpl(title, original_title, 1);
                    mtpl.Append("По-умолчанию", accsArgs($"{host}/lite/videoseed/video/{AesTo.Encrypt(cache.iframe)}") + "#.m3u8", "call", vast: init.vast);

                    return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                    #endregion
                }
                else
                {
                    #region Сериал
                    string enc_title = HttpUtility.UrlEncode(title);
                    string enc_original_title = HttpUtility.UrlEncode(original_title);

                    if (s == -1)
                    {
                        var tpl = new SeasonTpl(cache.seasons.Count);

                        foreach (var season in cache.seasons)
                        {
                            string link = $"{host}/lite/videoseed?rjson={rjson}&kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&title={enc_title}&original_title={enc_original_title}&s={season.Key}";
                            tpl.Append($"{season.Key} сезон", link, season.Key);
                        }

                        return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                    }
                    else
                    {
                        string sArhc = s.ToString();
                        var videos = cache.seasons.First(i => i.Key == sArhc).Value["videos"].ToObject<Dictionary<string, JObject>>();

                        var etpl = new EpisodeTpl(videos.Count);

                        foreach (var video in videos)
                        {
                            string iframe = video.Value.Value<string>("iframe");
                            etpl.Append($"{video.Key} серия", title ?? original_title, sArhc, video.Key, accsArgs($"{host}/lite/videoseed/video/{AesTo.Encrypt(iframe)}"), "call", vast: init.vast);
                        }

                        return ContentTo(rjson ? etpl.ToJson() : etpl.ToHtml());
                    }
                    #endregion
                }
            });
        }


        #region Video
        [HttpGet]
        [Route("lite/videoseed/video/{*iframe}")]
        async public ValueTask<ActionResult> Video(string iframe)
        {
            var init = await loadKit(AppInit.conf.Videoseed);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            iframe = AesTo.Decrypt(iframe);
            if (string.IsNullOrEmpty(iframe))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string memKey = $"videoseed:video:{iframe}:{proxyManager.CurrentProxyIp}";

            return await InvkSemaphore(init, memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out string location))
                {
                    var headers = httpHeaders(init);

                    try
                    {
                        using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                        {
                            var page = await browser.NewPageAsync(init.plugin, proxy: proxy.data, headers: headers?.ToDictionary()).ConfigureAwait(false);
                            if (page == null)
                                return null;

                            await page.AddInitScriptAsync("localStorage.setItem('pljsquality', '1080p');").ConfigureAwait(false);

                            await page.RouteAsync("**/*", async route =>
                            {
                                try
                                {
                                    if (!string.IsNullOrEmpty(location))
                                    {
                                        await route.AbortAsync();
                                        return;
                                    }

                                    if (route.Request.Url.Contains(".m3u8") || (route.Request.Url.Contains(".mp4") && !route.Request.Url.Contains(".ts")))
                                        location = route.Request.Url;

                                    if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                        return;

                                    await route.ContinueAsync();
                                }
                                catch { }
                            });

                            var options = new PageGotoOptions()
                            {
                                Timeout = 15_000,
                                WaitUntil = WaitUntilState.NetworkIdle
                            };

                            var result = await page.GotoAsync(iframe, options).ConfigureAwait(false);
                            if (result != null && string.IsNullOrEmpty(location))
                            {
                                string html = await page.ContentAsync().ConfigureAwait(false);
                                location = Regex.Match(html, "<video preload=\"none\" src=\"(https?://[^\"]+)\"").Groups[1].Value;
                                if (!location.Contains(".m3u") && !location.Contains(".mp4"))
                                    location = null;
                            }

                            PlaywrightBase.WebLog("SET", iframe, location, proxy.data);
                        }

                        if (string.IsNullOrEmpty(location))
                        {
                            proxyManager.Refresh();
                            return OnError();
                        }
                    }
                    catch
                    {
                        return OnError();
                    }

                    proxyManager.Success();
                    hybridCache.Set(memKey, location, cacheTime(20));
                }

                string link = HostStreamProxy(init, location, proxy: proxy.proxy, headers: HeadersModel.Init("referer", iframe));
                return ContentTo(VideoTpl.ToJson("play", link, "auto", vast: init.vast));
            });
        }
        #endregion
    }
}

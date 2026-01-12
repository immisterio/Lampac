using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class Videoseed : BaseOnlineController
    {
        public Videoseed() : base(AppInit.conf.Videoseed) { }

        [HttpGet]
        [Route("lite/videoseed")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int year, int s = -1, bool rjson = false, int serial = -1)
        {
            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                return OnError();

            if (await IsRequestBlocked(rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.token))
                return OnError();

            return await InvkSemaphore($"videoseed:view:{kinopoisk_id}:{imdb_id}:{original_title}", async key =>
            {
                #region search
                if (!hybridCache.TryGetValue(key, out (Dictionary<string, JObject> seasons, string iframe) cache))
                {
                    var data = await goSearch(serial, kinopoisk_id > 0, $"&kp={kinopoisk_id}") 
                        ?? await goSearch(serial, !string.IsNullOrEmpty(imdb_id), $"&tmdb={imdb_id}") 
                        ?? await goSearch(serial, !string.IsNullOrEmpty(original_title), $"&q={HttpUtility.UrlEncode(original_title)}&release_year_from={year - 1}&release_year_to={year + 1}");

                    if (data == null)
                    {
                        proxyManager?.Refresh();
                        return OnError();
                    }

                    if (serial == 1)
                        cache.seasons = data?["seasons"]?.ToObject<Dictionary<string, JObject>>();
                    else
                        cache.iframe = data?.Value<string>("iframe");

                    if (cache.seasons == null && string.IsNullOrEmpty(cache.iframe))
                    {
                        proxyManager?.Refresh();
                        return OnError();
                    }

                    proxyManager?.Success();
                    hybridCache.Set(key, cache, cacheTime(40));
                }
                #endregion

                if (cache.iframe != null)
                {
                    #region Фильм
                    var mtpl = new MovieTpl(title, original_title, 1);
                    mtpl.Append("По-умолчанию", accsArgs($"{host}/lite/videoseed/video/{AesTo.Encrypt(cache.iframe)}") + "#.m3u8", "call", vast: init.vast);

                    return await ContentTpl(mtpl);
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

                        return await ContentTpl(tpl);
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

                        return await ContentTpl(etpl);
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
            if (await IsRequestBlocked(rch: false))
                return badInitMsg;

            iframe = AesTo.Decrypt(iframe);
            if (string.IsNullOrEmpty(iframe))
                return OnError();

            return await InvkSemaphore($"videoseed:video:{iframe}:{proxyManager?.CurrentProxyIp}", async key =>
            {
                if (!hybridCache.TryGetValue(key, out string location))
                {
                    var headers = httpHeaders(init);

                    try
                    {
                        using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                        {
                            var page = await browser.NewPageAsync(init.plugin, proxy: proxy_data, headers: headers?.ToDictionary()).ConfigureAwait(false);
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

                            PlaywrightBase.WebLog("SET", iframe, location, proxy_data);
                        }

                        if (string.IsNullOrEmpty(location))
                        {
                            proxyManager?.Refresh();
                            return OnError();
                        }
                    }
                    catch
                    {
                        return OnError();
                    }

                    proxyManager?.Success();
                    hybridCache.Set(key, location, cacheTime(20));
                }

                string link = HostStreamProxy(location, headers: HeadersModel.Init("referer", iframe));
                return ContentTo(VideoTpl.ToJson("play", link, "auto", vast: init.vast));
            });
        }
        #endregion

        #region goSearch
        async Task<JToken> goSearch(int serial, bool isOk, string arg)
        {
            if (!isOk)
                return null;

            var root = await httpHydra.Get<JObject>($"{init.corsHost()}/apiv2.php?item={(serial == 1 ? "serial" : "movie")}&token={init.token}" + arg, safety: true);

            if (root == null || !root.ContainsKey("data") || root.Value<string>("status") == "error")
            {
                proxyManager?.Refresh();
                return null;
            }

            return root["data"]?.First;
        }
        #endregion
    }
}

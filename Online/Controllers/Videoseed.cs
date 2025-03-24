using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using System.Web;
using Newtonsoft.Json.Linq;
using Microsoft.Playwright;
using Shared.Engine;
using System.Collections.Generic;
using System.Linq;
using Shared.Model.Online;
using Shared.PlaywrightCore;

namespace Lampac.Controllers.LITE
{
    public class Videoseed : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/videoseed")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int s = -1, bool rjson = false, int serial = -1)
        {
            var init = await loadKit(AppInit.conf.Videoseed);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (kinopoisk_id == 0 || string.IsNullOrEmpty(init.token))
                return OnError();

            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            #region search
            string memKey = $"videoseed:view:{kinopoisk_id}";
            if (!hybridCache.TryGetValue(memKey, out (Dictionary<string, JObject> seasons, JToken translation_iframe, string iframe) cache))
            {
                string uri = $"{init.host}/apiv2.php?item={(serial == 1 ? "serial" : "movie")}&token={init.token}&kp={kinopoisk_id}";
                var root = await HttpClient.Get<JObject>(uri, timeoutSeconds: 8, headers: httpHeaders(init), proxy: proxy.proxy);

                if (root == null || !root.ContainsKey("data") || root.Value<string>("status") == "error")
                {
                    proxyManager.Refresh();
                    return OnError();
                }

                var data = root["data"]?.First;

                if (serial == 1)
                    cache.seasons = data?["seasons"]?.ToObject<Dictionary<string, JObject>>();
                else
                {
                    cache.translation_iframe = data?["translation_iframe"];
                    cache.iframe = data?.Value<string>("iframe");
                }

                if (cache.seasons == null && cache.translation_iframe == null && string.IsNullOrEmpty(cache.iframe))
                {
                    proxyManager.Refresh();
                    return OnError();
                }

                proxyManager.Success();
                hybridCache.Set(memKey, cache, cacheTime(40, init: init));
            }
            #endregion

            if (cache.translation_iframe != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);

                foreach (var translation in cache.translation_iframe)
                {
                    string iframe = translation.First.Value<string>("iframe");
                    mtpl.Append(translation.First.Value<string>("name"), accsArgs($"{host}/lite/videoseed/video?iframe={HttpUtility.UrlEncode(iframe)}"), vast: init.vast);
                }

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
            else if (cache.iframe != null)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);
                mtpl.Append("По-умолчанию", accsArgs($"{host}/lite/videoseed/video?iframe={HttpUtility.UrlEncode(cache.iframe)}"), vast: init.vast);

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
                        string link = $"{host}/lite/videoseed?rjson={rjson}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&s={season.Key}";
                        tpl.Append($"{season.Key} сезон", link, season.Key);
                    }

                    return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                }
                else
                {
                    var etpl = new EpisodeTpl();

                    foreach (var video in cache.seasons.First(i => i.Key == s.ToString()).Value["videos"].ToObject<Dictionary<string, JObject>>())
                    {
                        string iframe = video.Value.Value<string>("iframe");
                        etpl.Append($"{video.Key} серия", title ?? original_title, s.ToString(), video.Key, accsArgs($"{host}/lite/videoseed/video?iframe={HttpUtility.UrlEncode(iframe)}"), vast: init.vast);
                    }

                    return ContentTo(rjson ? etpl.ToJson() : etpl.ToHtml());
                }
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/videoseed/video")]
        async public Task<ActionResult> Video(string iframe)
        {
            var init = await loadKit(AppInit.conf.Videoseed);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(iframe))
                return OnError();

            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string memKey = $"videoseed:video:{iframe}:{proxyManager.CurrentProxyIp}";
            if (!hybridCache.TryGetValue(memKey, out string location))
            {
                try
                {
                    using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                    {
                        var page = await browser.NewPageAsync(init.plugin, proxy: proxy.data);
                        if (page == null)
                            return null;

                        await page.AddInitScriptAsync("localStorage.setItem('pljsquality', '1080p');");

                        await page.RouteAsync("**/*", async route =>
                        {
                            try
                            {
                                if (Regex.IsMatch(route.Request.Url, "/(embed|player)/"))
                                {
                                    if (await PlaywrightBase.AbortOrCache(memoryCache, page, route, abortMedia: true, fullCacheJS: true))
                                        return;

                                    await route.ContinueAsync();
                                    return;
                                }

                                await route.AbortAsync();
                            }
                            catch { }
                        });

                        var result = await page.GotoAsync(iframe);
                        if (result == null)
                            return OnError();

                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                        string html = await page.ContentAsync();

                        PlaywrightBase.WebLog("GET", iframe, html, proxy.data);

                        string video = Regex.Match(html ?? "", "<vide[^>]+ src=\"([^\"]+)").Groups[1].Value.Trim();
                        if (string.IsNullOrEmpty(video))
                        {
                            proxyManager.Refresh();
                            return OnError();
                        }

                        location = video;
                        proxyManager.Success();
                    }
                }
                catch
                {
                    return OnError();
                }

                hybridCache.Set(memKey, location, cacheTime(20));
            }

            return Redirect(HostStreamProxy(init, location, proxy: proxy.proxy, headers: HeadersModel.Init("referer", iframe)));
        }
        #endregion
    }
}

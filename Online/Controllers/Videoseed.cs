using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using System.Web;
using Newtonsoft.Json.Linq;
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
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int year, int s = -1, bool rjson = false, int serial = -1)
        {
            var init = await loadKit(AppInit.conf.Videoseed);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.token))
                return OnError();

            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled && init.priorityBrowser != "http")
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            #region search
            string memKey = $"videoseed:view:{kinopoisk_id}:{imdb_id}:{original_title}";
            if (!hybridCache.TryGetValue(memKey, out (Dictionary<string, JObject> seasons, string iframe) cache))
            {
                #region goSearch
                async ValueTask<JToken> goSearch(bool isOk, string arg)
                {
                    if (!isOk)
                        return null;

                    string uri = $"{init.host}/apiv2.php?item={(serial == 1 ? "serial" : "movie")}&token={init.token}" + arg;
                    var root = await HttpClient.Get<JObject>(uri, timeoutSeconds: 8, headers: httpHeaders(init), proxy: proxy.proxy);

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
                           await goSearch(!string.IsNullOrEmpty(original_title), $"&q={HttpUtility.UrlEncode(original_title)}&release_year_from={year-1}&release_year_to={year+1}");

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
                var mtpl = new MovieTpl(title, original_title);
                mtpl.Append("По-умолчанию", accsArgs($"{host}/lite/videoseed/video/{AesTo.Encrypt(cache.iframe)}"), vast: init.vast);

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
                    var etpl = new EpisodeTpl();

                    foreach (var video in cache.seasons.First(i => i.Key == s.ToString()).Value["videos"].ToObject<Dictionary<string, JObject>>())
                    {
                        string iframe = video.Value.Value<string>("iframe");
                        etpl.Append($"{video.Key} серия", title ?? original_title, s.ToString(), video.Key, accsArgs($"{host}/lite/videoseed/video/{AesTo.Encrypt(iframe)}"), vast: init.vast);
                    }

                    return ContentTo(rjson ? etpl.ToJson() : etpl.ToHtml());
                }
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/videoseed/video/{*iframe}")]
        async public Task<ActionResult> Video(string iframe)
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
            if (!hybridCache.TryGetValue(memKey, out string location))
            {
                var headers = httpHeaders(init);

                try
                {
                    if (init.priorityBrowser == "http")
                    {
                        string html = await HttpClient.Get(iframe, httpversion: 2, timeoutSeconds: 8, proxy: proxy.proxy, headers: headers);
                        if (html == null)
                        {
                            proxyManager.Refresh();
                            return OnError();
                        }

                        foreach (string q in new string[] { "1080p", "720p", "480p" })
                        {
                            location = Regex.Match(html, $"\\[{q}]({{[^}}]+}} )?(https?://[^,;\t\n\r ]+\\.mp4/)").Groups[2].Value.Trim();
                            if (!string.IsNullOrEmpty(location))
                                break;
                        }
                    }
                    else
                    {
                        #region PlaywrightBrowser
                        using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                        {
                            var page = await browser.NewPageAsync(init.plugin, proxy: proxy.data, headers: headers?.ToDictionary());
                            if (page == null)
                                return null;

                            await page.AddInitScriptAsync("localStorage.setItem('pljsquality', '1080p');");

                            await page.RouteAsync("**/*", async route =>
                            {
                                try
                                {
                                    if (Regex.IsMatch(route.Request.Url, "/(embed|player)/"))
                                    {
                                        if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                            return;

                                        await route.ContinueAsync();
                                        return;
                                    }

                                    await route.AbortAsync();
                                }
                                catch { }
                            });

                            PlaywrightBase.GotoAsync(page, iframe);
                            await page.WaitForSelectorAsync(".pjscssed");
                            string html = await page.ContentAsync();

                            PlaywrightBase.WebLog("GET", iframe, html, proxy.data);

                            location = Regex.Match(html ?? "", "<vide[^>]+ src=\"([^\"]+)").Groups[1].Value.Trim();
                        }
                        #endregion
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

            return Redirect(HostStreamProxy(init, location, proxy: proxy.proxy, headers: HeadersModel.Init("referer", iframe)));
        }
        #endregion
    }
}

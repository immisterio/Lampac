using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using Shared.Engine;
using Lampac.Models.LITE;
using System;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Lampac.Engine.CORE;
using System.Web;

namespace Lampac.Controllers.LITE
{
    public class AutoEmbed : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/autoembed")]
        async public Task<ActionResult> Index(bool checksearch, long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            if (checksearch)
                return Content("data-json=");

            var init = await loadKit(AppInit.conf.Autoembed);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(imdb_id))
                return OnError();

            if (Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

            if (serial == 1)
            {
                #region Сериал
                var tmdb = await InvokeCache<JToken>($"tmdb:seasons:{id}", cacheTime(40, init: init), async res =>
                {
                    var root = await HttpClient.Get<JObject>($"{AppInit.conf.cub.scheme}://tmdb.{AppInit.conf.cub.mirror}/3/tv/{id}?api_key={AppInit.conf.tmdb.api_key}");

                    if (root == null || !root.ContainsKey("seasons"))
                        return res.Fail("seasons");

                    return root["seasons"];
                });

                if (!tmdb.IsSuccess)
                    return OnError(tmdb.ErrorMsg);

                if (s == -1)
                {
                    #region Сезоны
                    var tpl = new SeasonTpl();

                    foreach (var season in tmdb.Value)
                    {
                        int number = season.Value<int>("season_number");
                        if (1 > number)
                            continue;

                        string link = $"{host}/lite/autoembed?id={id}&imdb_id={imdb_id}&serial=1&rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={number}";
                        tpl.Append($"{number} сезон", link, number);
                    }

                    return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                    #endregion
                }
                else
                {
                    #region Серии
                    var etpl = new EpisodeTpl();

                    foreach (var season in tmdb.Value)
                    {
                        if (season.Value<int>("season_number") != s)
                            continue;

                        for (int i = 1; i <= season.Value<int>("episode_count"); i++)
                            etpl.Append($"{i} серия", title ?? original_title, s.ToString(), i.ToString(), accsArgs($"{host}/lite/autoembed/video.mp4?id={id}&s={s}&e={i}"), vast: init.vast);
                    }

                    return ContentTo(rjson ? etpl.ToJson() : etpl.ToHtml());
                    #endregion
                }
                #endregion
            }
            else
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);

                mtpl.Append("480p", accsArgs($"{host}/lite/autoembed/video.mp4?id={id}"), vast: init.vast);

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/autoembed/video.mp4")]
        async public Task<ActionResult> Video(long id, int s = -1, int e = -1)
        {
            var init = await loadKit(AppInit.conf.Autoembed);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/embed/movie/{id}?server=1";
            if (s > 0)
                embed = $"{init.host}/embed/tv/{id}/{s}/{e}?server=1";

            string hls = await black_magic(embed, init, proxy.data);
            if (hls == null)
                return StatusCode(502);

            return Redirect(HostStreamProxy(init, hls, proxy: proxy.proxy));
        }
        #endregion


        #region black_magic
        async ValueTask<string> black_magic(string uri, OnlinesSettings init, (string ip, string username, string password) proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return uri;

            try
            {
                string memKey = $"autoembed:black_magic:{uri}";
                if (!memoryCache.TryGetValue(memKey, out string mp4))
                {
                    using (var browser = new Firefox())
                    {
                        var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy);
                        if (page == null)
                            return null;

                        await page.RouteAsync("**/*", async route =>
                        {
                            Console.WriteLine($"Firefox: {route.Request.Method} {route.Request.Url}");

                            if (route.Request.Url.Contains("hakunaymatata.") && route.Request.Url.Contains(".mp4"))
                            {
                                browser.completionSource.SetResult(route.Request.Url);
                                await route.AbortAsync();
                                return;
                            }

                            if (route.Request.Url.Contains("/ads/"))
                            {
                                await route.AbortAsync();
                                return;
                            }

                            await PlaywrightBase.CacheOrContinue(memoryCache, page, route);
                        });

                        _ = page.GotoAsync(uri).ConfigureAwait(false);
                        mp4 = await browser.WaitPageResult(30);
                    }

                    if (mp4 == null)
                        return null;

                    memoryCache.Set(memKey, mp4, cacheTime(20, init: init));
                }

                return mp4;
            }
            catch { return null; }
        }
        #endregion
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using Microsoft.Playwright;
using Shared.Engine;
using Lampac.Models.LITE;
using System;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Lampac.Engine.CORE;
using System.Web;

namespace Lampac.Controllers.LITE
{
    public class Vidsrc : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/vidsrc")]
        async public Task<ActionResult> Index(long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Vidsrc);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(imdb_id))
                return OnError();

            if (Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

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

                        string link = $"{host}/lite/vidsrc?id={id}&imdb_id={imdb_id}&serial=1&rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={number}";
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
                            etpl.Append($"{i} серия", title ?? original_title, s.ToString(), i.ToString(), accsArgs($"{host}/lite/vidsrc/video.m3u8?imdb_id={imdb_id}&s={s}&e={i}"), vast: init.vast);
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

                mtpl.Append("1080p", accsArgs($"{host}/lite/vidsrc/video.m3u8?imdb_id={imdb_id}"), vast: init.vast);

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/vidsrc/video.m3u8")]
        async public Task<ActionResult> Video(string imdb_id, int s = -1, int e = -1)
        {
            var init = await loadKit(AppInit.conf.Vidsrc);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(imdb_id))
                return OnError();

            if (Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/embed/movie/{imdb_id}";
            if (s > 0)
                embed = $"{init.host}/embed/tv/{imdb_id}/{s}/{e}";

            string hls = await black_magic(embed, init, proxy.data);
            if (hls == null)
                return StatusCode(502);

            return Redirect(HostStreamProxy(init, hls, proxy: proxy.proxy));
        }
        #endregion


        #region black_magic
        async ValueTask<string> black_magic(string uri, OnlinesSettings init, (string ip, string username, string password) proxy)
        {
            try
            {
                string memKey = $"vidsrc:black_magic:{uri}";
                if (!memoryCache.TryGetValue(memKey, out string m3u8))
                {
                    using (var browser = new Firefox())
                    {
                        var page = await browser.NewPageAsync(proxy: proxy);
                        if (page == null)
                            return null;

                        await page.SetExtraHTTPHeadersAsync(httpHeaders(init).ToDictionary());

                        await page.RouteAsync("**/*", async route =>
                        {
                            if (m3u8 != null || route.Request.Url.Contains(".css"))
                            {
                                await route.AbortAsync();
                                return;
                            }

                            if (route.Request.Url.Contains("master.m3u8"))
                            {
                                m3u8 = route.Request.Url;
                                await route.AbortAsync();
                                return;
                            }

                            await route.ContinueAsync();
                        });

                        var response = await page.GotoAsync(uri);
                        if (response == null)
                            return null;

                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                        var viewportSize = await page.EvaluateAsync<ViewportSize>("() => ({ width: window.innerWidth, height: window.innerHeight })");

                        for (int i = 0; i < 10; i++)
                        {
                            if (m3u8 != null)
                                break;

                            int vS(int center)
                            {
                                var centerX = center / 2;
                                return Random.Shared.Next(0, 2) == 1 ? (centerX + Random.Shared.Next(1, 5)) : (centerX - Random.Shared.Next(1, 5));
                            }

                            await page.Mouse.ClickAsync(vS(viewportSize.Width), vS(viewportSize.Height));
                            await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(20, 50)));
                        }

                        if (m3u8 == null)
                            return null;
                    }

                    memoryCache.Set(memKey, m3u8, cacheTime(20, init: init));
                }

                return m3u8;
            }
            catch { return null; }
        }
        #endregion
    }
}

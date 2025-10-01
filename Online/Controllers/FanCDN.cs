using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Shared.PlaywrightCore;
using System.Net;
using BrowserCookie = Microsoft.Playwright.Cookie;
using Microsoft.AspNetCore.Routing;
using Shared.Models.Online.Settings;
using Shared.Models.Online.FanCDN;

namespace Online.Controllers
{
    public class FanCDN : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/fancdn")]
        async public ValueTask<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int year, int serial, int t = -1, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.FanCDN);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.token) && string.IsNullOrEmpty(init.cookie))
                return OnError();

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            var oninvk = new FanCDNInvoke
            (
               host,
               init.host,
               async ongettourl => 
               {
                   if (ongettourl.Contains("fancdn."))
                       return await black_magic(init, rch, init.cors(ongettourl), proxy);

                   if (string.IsNullOrEmpty(init.cookie))
                       return null;

                   var headers = httpHeaders(init, HeadersModel.Init(
                       ("sec-fetch-dest", "document"),
                       ("sec-fetch-mode", "navigate"),
                       ("sec-fetch-site", "none"),
                       ("cookie", init.cookie)
                   ));

                   if (rch.enable)
                       return await rch.Get(init.cors(ongettourl), headers);

                   if (init.priorityBrowser == "http")
                       return await Http.Get(init.cors(ongettourl), httpversion: 2, timeoutSeconds: 8, proxy: proxy.proxy, headers: headers);

                   #region Browser Search
                   try
                   {
                       using (var browser = new PlaywrightBrowser())
                       {
                           var page = await browser.NewPageAsync(init.plugin, proxy: proxy.data);
                           if (page == null)
                               return null;

                           string fanhost = "." + Regex.Replace(init.host, "^https?://", "");
                           var excookie = DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeSeconds();

                           var cookies = new List<BrowserCookie>();
                           foreach (string line in init.cookie.Split(";"))
                           {
                               if (string.IsNullOrEmpty(line) || !line.Contains("=") || line.Contains("cf_clearance") || line.Contains("PHPSESSID"))
                                   continue;

                               cookies.Add(new BrowserCookie()
                               {
                                   Domain = fanhost,
                                   Expires = excookie,
                                   Path = "/",
                                   HttpOnly = true,
                                   Secure = true,
                                   Name = line.Split("=")[0].Trim(),
                                   Value = line.Split("=")[1].Trim()
                               });
                           }

                           await page.Context.AddCookiesAsync(cookies);

                           var response = await page.GotoAsync(ongettourl, new PageGotoOptions()
                           {
                               Timeout = 10_000,
                               WaitUntil = WaitUntilState.DOMContentLoaded 
                           });

                           if (response == null)
                               return null;

                           string result = await response.TextAsync();
                           PlaywrightBase.WebLog("GET", ongettourl, result, proxy.data, response: response);
                           return result;
                       }
                   }
                   catch
                   {
                       return null;
                   }
                   #endregion
               },
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy.proxy)
            );

            reset:
            var cache = await InvokeCache<EmbedModel>(rch.ipkey($"fancdn:{title}", proxyManager), cacheTime(20, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                var result = !string.IsNullOrEmpty(init.token) && kinopoisk_id > 0 ? await oninvk.EmbedToken(kinopoisk_id, init.token) : await oninvk.EmbedSearch(title, original_title, year, serial);
                if (result == null)
                    return res.Fail("result");

                return result;
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, imdb_id, kinopoisk_id, title, original_title, t, s, rjson: rjson, vast: init.vast, headers: httpHeaders(init)), origsource: origsource);
        }


        #region black_magic
        async Task<string> black_magic(OnlinesSettings init, RchClient rch, string uri, (WebProxy proxy, (string ip, string username, string password) data) baseproxy)
        {
            try
            {
                var headers = httpHeaders(init, HeadersModel.Init(
                    ("sec-fetch-dest", "iframe"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "cross-site"),
                    ("referer", $"{init.host}/")
                ));

                if (rch.enable)
                    return await rch.Get(uri, headers);

                if (init.priorityBrowser == "http")
                    return await Http.Get(uri, httpversion: 2, timeoutSeconds: 8, proxy: baseproxy.proxy, headers: headers);

                using (var browser = new PlaywrightBrowser())
                {
                    var page = await browser.NewPageAsync(init.plugin, init.headers, proxy: baseproxy.data, imitationHuman: init.imitationHuman);
                    if (page == null)
                        return null;

                    browser.SetFailedUrl(uri);

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            if (route.Request.Url.StartsWith(init.host))
                            {
                                await route.FulfillAsync(new RouteFulfillOptions
                                {
                                    Body = PlaywrightBase.IframeHtml(uri)
                                });
                            }
                            else if (route.Request.Url == uri)
                            {
                                string html = null;
                                await browser.ClearContinueAsync(route, page);

                                var response = await page.WaitForResponseAsync(route.Request.Url);
                                if (response != null)
                                    html = await response.TextAsync();

                                browser.SetPageResult(html);
                                PlaywrightBase.WebLog(route.Request, response, html, baseproxy.data);
                            }
                            else
                            {
                                if (!init.imitationHuman || route.Request.Url.EndsWith(".m3u8"))
                                {
                                    PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                }
                                else
                                {
                                    if (await PlaywrightBase.AbortOrCache(page, route))
                                        return;

                                    await browser.ClearContinueAsync(route, page);
                                }
                            }
                        }
                        catch { }
                    });

                    PlaywrightBase.GotoAsync(page, init.host);
                    return await browser.WaitPageResult();
                }
            }
            catch
            {
                return null; 
            }
        }
        #endregion
    }
}

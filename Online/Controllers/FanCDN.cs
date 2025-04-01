using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using Shared.Model.Online.FanCDN;
using Shared.Engine.Online;
using Shared.Engine;
using Lampac.Models.LITE;
using Microsoft.Playwright;
using Shared.Engine.CORE;
using System;
using Shared.PlaywrightCore;
using Lampac.Engine.CORE;
using Shared.Model.Online;
using System.Net;

namespace Lampac.Controllers.LITE
{
    public class FanCDN : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/fancdn")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int year, int serial, int t = -1, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.FanCDN);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.cookie) || kinopoisk_id == 0)
                return OnError();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            var oninvk = new FanCDNInvoke
            (
               host,
               init.host,
               ongettourl => 
               {
                   if (ongettourl.Contains("fancdn."))
                       return black_magic(init, rch, init.cors(ongettourl), proxy);

                   var headers = httpHeaders(init, HeadersModel.Init(
                       ("sec-fetch-dest", "document"),
                       ("sec-fetch-mode", "navigate"),
                       ("sec-fetch-site", "same-origin")
                   ));

                   if (ongettourl.Contains("do=search"))
                       headers.Add(new HeadersModel("referer", $"{init.host}/"));

                   if (rch.enable)
                       return rch.Get(init.cors(ongettourl), HeadersModel.Join(headers, HeadersModel.Init("cookie", init.cookie)));

                   return HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy.proxy, cookie: init.cookie, headers: headers);
               },
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy.proxy)
            );

            var cache = await InvokeCache<EmbedModel>(rch.ipkey($"fancdn:{title}", proxyManager), cacheTime(20, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                var result = await oninvk.Embed(imdb_id, kinopoisk_id, title, original_title, year, serial);
                if (result == null)
                    return res.Fail(logRequest);

                return result;
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, imdb_id, kinopoisk_id, title, original_title, t, s, rjson: rjson, vast: init.vast), origsource: origsource);
        }


        #region black_magic
        string logRequest = string.Empty;

        async ValueTask<string> black_magic(OnlinesSettings init, RchClient rch, string uri, (WebProxy proxy, (string ip, string username, string password) data) baseproxy)
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
                    return await HttpClient.Get(uri, httpversion: 2, timeoutSeconds: 8, proxy: baseproxy.proxy, headers: headers);

                using (var browser = new PlaywrightBrowser())
                {
                    var page = await browser.NewPageAsync(init.plugin, proxy: baseproxy.data);
                    if (page == null)
                    {
                        logRequest += "\nNewPageAsync null";
                        return null;
                    }

                    browser.failedUrl = uri;

                    await page.Context.ClearCookiesAsync(new BrowserContextClearCookiesOptions { Domain = ".fancdn.net", Name = "cf_clearance" });

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            if (route.Request.Url.Contains("api/chromium/iframe"))
                            {
                                await route.ContinueAsync();
                                return;
                            }

                            if (browser.IsCompleted)
                            {
                                Console.WriteLine($"Playwright: Abort {route.Request.Url}");
                                await route.AbortAsync();
                                return;
                            }

                            logRequest += $"{route.Request.Method}: {route.Request.Url}\n";

                            if (route.Request.Url == uri)
                            {
                                string html = null;
                                await route.ContinueAsync(new RouteContinueOptions
                                {
                                    Headers = headers.ToDictionary()
                                });

                                var response = await page.WaitForResponseAsync(route.Request.Url);
                                if (response != null)
                                    html = await response.TextAsync();

                                browser.SetPageResult(html);
                                PlaywrightBase.WebLog(route.Request, response, html, baseproxy.data);
                                return;
                            }

                            await route.AbortAsync();
                        }
                        catch { }
                    });

                    var response = await page.GotoAsync(PlaywrightBase.IframeUrl(uri));
                    if (response == null)
                        return null;

                    return await browser.WaitPageResult();
                }
            }
            catch (Exception ex) 
            {
                logRequest += $"\n{ex.Message}";
                return null; 
            }
        }
        #endregion
    }
}

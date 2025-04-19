using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using System.Text.RegularExpressions;
using Shared.Engine.Online;
using Shared.Engine;
using Shared.Model.Online.VDBmovies;
using Microsoft.Playwright;
using Lampac.Models.LITE;
using Shared.Engine.CORE;
using Shared.PlaywrightCore;
using System;
using Lampac.Engine.CORE;
using System.Net;
using Shared.Model.Online;

namespace Lampac.Controllers.LITE
{
    public class VDBmovies : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/vdbmovies")]
        async public Task<ActionResult> Index(string title, string original_title, long kinopoisk_id, string t, int sid, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.VDBmovies);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (kinopoisk_id == 0)
                return OnError();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            var oninvk = new VDBmoviesInvoke
            (
               host,
               MaybeInHls(init.hls, init),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy.proxy)
            );

            var cache = await InvokeCache<EmbedModel>(rch.ipkey($"vdbmovies:{kinopoisk_id}", proxyManager), cacheTime(20, rhub: 2, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string uri = $"{init.corsHost()}/kinopoisk/{kinopoisk_id}/iframe";

                string html = rch.enable ? await rch.Get(uri, httpHeaders(init)) : 
                                           await black_magic($"{init.host}/kinopoisk/{kinopoisk_id}/iframe", init, proxy);

                if (html == null)
                    return res.Fail("html");

                string file = Regex.Match(html, "file:([\t ]+)?'(#[^']+)").Groups[2].Value;
                if (string.IsNullOrEmpty(file))
                    return res.Fail("file");

                //try
                //{
                //    using (var browser = await PuppeteerTo.Browser())
                //    {
                //        var page = await browser.MainPage();

                //        if (page == null)
                //            return null;

                //        return oninvk.Embed(await page.EvaluateExpressionAsync<string>(oninvk.EvalCode(file)));
                //    }
                //}
                //catch
                //{
                //    return null;
                //}

                return oninvk.Embed(oninvk.DecodeEval(file));
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, kinopoisk_id, title, original_title, t, s, sid, vast: init.vast, rjson: rjson), origsource: origsource, gbcache: !rch.enable);
        }


        #region black_magic
        async ValueTask<string> black_magic(string uri, OnlinesSettings init, (WebProxy proxy, (string ip, string username, string password) data) baseproxy)
        {
            try
            {
                string host = CrypTo.DecodeBase64("aHR0cHM6Ly9raW5vb25saW5lLWhkLmNvbQ==");

                var headers = httpHeaders(init, HeadersModel.Init(
                    ("sec-fetch-dest", "iframe"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "cross-site"),
                    ("referer", $"{host}/")
                ));

                if (init.priorityBrowser == "http")
                    return await HttpClient.Get(uri, httpversion: 2, timeoutSeconds: 8, proxy: baseproxy.proxy, headers: headers);

                using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                {
                    var page = await browser.NewPageAsync(init.plugin, proxy: baseproxy.data, imitationHuman: init.imitationHuman);
                    if (page == null)
                        return null;

                    browser.failedUrl = uri;
                    await page.SetExtraHTTPHeadersAsync(init.headers);

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            if (route.Request.Url.StartsWith(host))
                            {
                                await route.FulfillAsync(new RouteFulfillOptions
                                {
                                    Body = PlaywrightBase.IframeHtml(uri)
                                });
                            }
                            else if (route.Request.Url == uri)
                            {
                                string html = null;
                                await route.ContinueAsync();

                                var response = await page.WaitForResponseAsync(route.Request.Url);
                                if (response != null)
                                    html = await response.TextAsync();

                                browser.SetPageResult(html);
                                PlaywrightBase.WebLog(route.Request, response, html, baseproxy.data);
                                return;
                            }
                            else
                            {
                                if (!init.imitationHuman || route.Request.Url.EndsWith(".m3u8") || route.Request.Url.Contains("/cdn-cgi/challenge-platform/"))
                                {
                                    Console.WriteLine($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                }
                                else
                                {
                                    if (await PlaywrightBase.AbortOrCache(page, route))
                                        return;

                                    await route.ContinueAsync();
                                }
                            }
                        }
                        catch { }
                    });

                    PlaywrightBase.GotoAsync(page, host);
                    return await browser.WaitPageResult();
                }
            }
            catch { return null; }
        }
        #endregion
    }
}

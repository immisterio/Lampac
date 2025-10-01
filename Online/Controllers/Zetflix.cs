using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class Zetflix : BaseOnlineController
    {
        static string PHPSESSID = null;

        [HttpGet]
        [Route("lite/zetflix")]
        async public ValueTask<ActionResult> Index(long id, int serial, long kinopoisk_id, string title, string original_title, string t, int s = -1, bool orightml = false, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Zetflix);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (kinopoisk_id == 0)
                return OnError();

            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string ztfhost = await goHost(init.host);
            string log = $"{HttpContext.Request.Path.Value}\n\nstart init\n";

            var oninvk = new ZetflixInvoke
            (
               host,
               ztfhost,
               init.hls,
               (url, head) => Http.Get(init.cors(url), headers: httpHeaders(init, head), timeoutSeconds: 8, proxy: proxy.proxy),
               onstreamtofile => HostStreamProxy(init, onstreamtofile, proxy: proxy.proxy)
               //AppInit.log
            );

            int rs = serial == 1 ? (s == -1 ? 1 : s) : s;

            string html = await InvokeCache($"zetfix:view:{kinopoisk_id}:{rs}:{proxyManager.CurrentProxyIp}", cacheTime(20, init: init), async () => 
            {
                string uri = $"{ztfhost}/iplayer/videodb.php?kp={kinopoisk_id}" + (rs > 0 ? $"&season={rs}" : "");

                var headers = HeadersModel.Init(Chromium.baseContextOptions.ExtraHTTPHeaders.ToDictionary(), ("Referer", "https://www.google.com/"));

                string result = string.IsNullOrEmpty(PHPSESSID) ? null : await Http.Get(uri, proxy: proxy.proxy, cookie: $"PHPSESSID={PHPSESSID}", headers: headers);
                if (result != null && !result.StartsWith("<script>(function"))
                {
                    if (!result.Contains("new Playerjs"))
                        return null;

                    proxyManager.Success();
                    return result;
                }

                try
                {
                    using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                    {
                        log += "browser init\n";

                        var page = await browser.NewPageAsync(init.plugin, new Dictionary<string, string>()
                        {
                            ["Referer"] = "https://www.google.com/"

                        }, proxy: proxy.data, keepopen: init.browser_keepopen).ConfigureAwait(false);

                        if (page == null)
                            return null;

                        if (init.browser_keepopen)
                        {
                            await page.Context.ClearCookiesAsync(new BrowserContextClearCookiesOptions
                            {
                                Domain = Regex.Replace(ztfhost, "^https?://", ""),
                                Name = "PHPSESSID"

                            }).ConfigureAwait(false);
                        }

                        log += "page init\n";

                        await page.RouteAsync("**/*", async route =>
                        {
                            try
                            {
                                if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                    return;

                                await route.ContinueAsync();
                            }
                            catch { }
                        });

                        await page.GotoAsync(uri, new PageGotoOptions() 
                        {
                            Timeout = 15_000,
                            WaitUntil = WaitUntilState.NetworkIdle 
                        }).ConfigureAwait(false);

                        result = await page.ContentAsync().ConfigureAwait(false);

                        log += $"{result}\n\n";

                        if (result == null || result.StartsWith("<script>(function"))
                        {
                            proxyManager.Refresh();
                            return null;
                        }

                        var cook = await page.Context.CookiesAsync().ConfigureAwait(false);
                        PHPSESSID = cook?.FirstOrDefault(i => i.Name == "PHPSESSID")?.Value;

                        if (!result.Contains("new Playerjs"))
                            return null;

                        return result;
                    }
                }
                catch (Exception ex) 
                {
                    log += $"\nex: {ex}\n";
                    return null; 
                }
            });

            if (html == null)
                return OnError();

            if (orightml)
                return Content(html, "text/plain; charset=utf-8");

            var content = oninvk.Embed(html);
            if (content.pl == null)
                return OnError();

            if (origsource)
                return Json(content);

            int number_of_seasons = 1;
            if (!content.movie && s == -1 && id > 0)
                number_of_seasons = await InvokeCache($"zetfix:number_of_seasons:{kinopoisk_id}", cacheTime(120, init: init), () => oninvk.number_of_seasons(id));

            OnLog(log + "\nStart OnResult");

            return ContentTo(oninvk.Html(content, number_of_seasons, kinopoisk_id, title, original_title, t, s, vast: init.vast, rjson: rjson));
        }


        async ValueTask<string> goHost(string host)
        {
            if (!Regex.IsMatch(host, "^https?://go\\."))
                return host;

            string backhost = CrypTo.DecodeBase64("aHR0cHM6Ly96ZXQtZmxpeC5vbmxpbmU=");

            string memkey = $"zeflix:gohost:{host}";
            if (hybridCache.TryGetValue(memkey, out string ztfhost))
            {
                if (string.IsNullOrEmpty(ztfhost))
                    return backhost;

                return ztfhost;
            }

            string html = await Http.Get(host, timeoutSeconds: 8);
            if (html != null)
            {
                ztfhost = Regex.Match(html, "\"([^\"]+)\"\\);</script>").Groups[1].Value;
                if (!string.IsNullOrEmpty(ztfhost))
                {
                    ztfhost = $"https://{ztfhost}";
                    hybridCache.Set(memkey, ztfhost, DateTime.Now.AddMinutes(20));
                    return ztfhost;
                }
            }
            else
            {
                hybridCache.Set(memkey, string.Empty, DateTime.Now.AddMinutes(1));
            }

            return backhost;
        }
    }
}

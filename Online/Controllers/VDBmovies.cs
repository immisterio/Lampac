using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Shared.Models.Online.Settings;
using Shared.Models.Online.VDBmovies;
using Shared.PlaywrightCore;
using System.Net;

namespace Online.Controllers
{
    public class VDBmovies : BaseOnlineController
    {
        #region database
        static List<MovieDB> databaseCache;

        static IEnumerable<MovieDB> database
        {
            get
            {
                if (AppInit.conf.multiaccess || databaseCache != null)
                    return databaseCache ??= JsonHelper.ListReader<MovieDB>("data/cdnmovies.json", 105000);

                return JsonHelper.IEnumerableReader<MovieDB>("data/cdnmovies.json");
            }
        }
        #endregion

        static string referer = CrypTo.DecodeBase64("aHR0cHM6Ly9tb3ZpZWJvb20uc3RvcmUv");

        [HttpGet]
        [Route("lite/vdbmovies")]
        async public ValueTask<ActionResult> Index(string orid, string imdb_id, long kinopoisk_id, string title, string original_title, bool similar, string t, int sid, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.VDBmovies);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            var oninvk = new VDBmoviesInvoke
            (
               host,
               init.hls,
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy.proxy)
            );

            #region поиск
            if (similar || (string.IsNullOrEmpty(orid) && kinopoisk_id == 0))
            {
                if (!init.spider)
                    return OnError("spider");

                var stpl = new SimilarTpl();

                string stitle = StringConvert.SearchName(title);
                string sorigtitle = StringConvert.SearchName(original_title);

                foreach (var j in database)
                {
                    if (stpl.data.Count > 100)
                        break;

                    bool IsOkTitle = false, IsOkID = false;

                    if (kinopoisk_id > 0 && kinopoisk_id == j.kinopoisk_id)
                        IsOkID = true;

                    if (!string.IsNullOrEmpty(imdb_id) && j.imdb_id == imdb_id)
                        IsOkID = true;

                    if (!IsOkID)
                    {
                        if (sorigtitle != null && StringConvert.SearchName(j.orig_title) == sorigtitle)
                            IsOkTitle = true;

                        if (!IsOkTitle && stitle != null)
                        {
                            if (StringConvert.SearchName(j.ru_title) != null)
                            {
                                if (StringConvert.SearchName(j.ru_title).Contains(stitle))
                                    IsOkTitle = true;
                            }

                            if (!IsOkTitle && StringConvert.SearchName(j.orig_title) != null)
                            {
                                if (StringConvert.SearchName(j.orig_title).Contains(stitle))
                                    IsOkTitle = true;
                            }
                        }
                    }

                    if (IsOkTitle || IsOkID)
                    {
                        if (!similar && IsOkID)
                        {
                            orid = j.id;
                            break;
                        }
                        else
                        {
                            string uri = $"{host}/lite/vdbmovies?orid={j.id}";
                            stpl.Append(j.ru_title ?? j.orig_title, j.year.ToString(), string.Empty, uri, PosterApi.Find(j.kinopoisk_id, j.imdb_id));
                        }
                    }
                }

                if (similar || string.IsNullOrEmpty(orid))
                    return ContentTo(rjson ? stpl.ToJson() : stpl.ToHtml());
            }
            #endregion

            reset: 
            var cache = await InvokeCache<EmbedModel>(rch.ipkey($"vdbmovies:{orid}:{kinopoisk_id}", proxyManager), cacheTime(20, rhub: 2, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string uri = $"{init.corsHost()}/kinopoisk/{kinopoisk_id}/iframe";
                if (!string.IsNullOrEmpty(orid))
                    uri = $"{init.corsHost()}/content/{orid}/iframe";

                string html = rch.enable ? await rch.Get(uri, httpHeaders(init, HeadersModel.Init(("referer", referer)))) : 
                                           await black_magic(uri, referer, init, proxy);

                if (html == null)
                    return res.Fail("html");

                string file = Regex.Match(html, "file:([\t ]+)?'(#[^']+)").Groups[2].Value;
                if (string.IsNullOrEmpty(file))
                    return res.Fail("file");

                return oninvk.Embed(oninvk.DecodeEval(file));
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, orid, imdb_id, kinopoisk_id, title, original_title, t, s, sid, vast: init.vast, rjson: rjson), origsource: origsource, gbcache: !rch.enable);
        }


        #region black_magic
        async Task<string> black_magic(string uri, string referer, OnlinesSettings init, (WebProxy proxy, (string ip, string username, string password) data) baseproxy)
        {
            try
            {
                var headers = httpHeaders(init, HeadersModel.Init(
                    ("sec-fetch-dest", "iframe"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "cross-site"),
                    ("referer", referer)
                ));

                if (init.priorityBrowser == "http")
                    return await Http.Get(uri, httpversion: 2, timeoutSeconds: 8, proxy: baseproxy.proxy, headers: headers);

                using (var browser = new PlaywrightBrowser(init.priorityBrowser))
                {
                    var page = await browser.NewPageAsync(init.plugin, init.headers, proxy: baseproxy.data, imitationHuman: init.imitationHuman).ConfigureAwait(false);
                    if (page == null)
                        return null;

                    browser.SetFailedUrl(uri);

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            if (route.Request.Url.StartsWith(referer))
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
                                    PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
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

                    PlaywrightBase.GotoAsync(page, referer);
                    return await browser.WaitPageResult().ConfigureAwait(false);
                }
            }
            catch { return null; }
        }
        #endregion
    }
}

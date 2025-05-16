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
using System.Collections.Generic;
using Shared.Model.Base;
using Shared.Model.Templates;

namespace Lampac.Controllers.LITE
{
    public class VDBmovies : BaseOnlineController
    {
        public static List<MovieDB> database = null;

        [HttpGet]
        [Route("lite/vdbmovies")]
        async public Task<ActionResult> Index(string orid, string imdb_id, long kinopoisk_id, string title, string original_title, bool similar, string t, int sid, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.VDBmovies);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
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

                if (database == null)
                    database = JsonHelper.ListReader<MovieDB>("data/cdnmovies.json", 105000);

                string NormalizeString(string str) => Regex.Replace(str?.ToLower() ?? "", "[^a-zа-я0-9]", "");

                var stpl = new SimilarTpl();

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
                        if (!string.IsNullOrEmpty(NormalizeString(original_title)) && NormalizeString(j.orig_title) == NormalizeString(original_title))
                            IsOkTitle = true;

                        if (!IsOkTitle && !string.IsNullOrEmpty(NormalizeString(title)))
                        {
                            if (!string.IsNullOrEmpty(j.ru_title))
                            {
                                if (NormalizeString(j.ru_title).Contains(NormalizeString(title)))
                                    IsOkTitle = true;
                            }

                            if (!IsOkTitle && !string.IsNullOrEmpty(j.orig_title))
                            {
                                if (NormalizeString(j.orig_title).Contains(NormalizeString(title)))
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

            var cache = await InvokeCache<EmbedModel>(rch.ipkey($"vdbmovies:{orid}:{kinopoisk_id}", proxyManager), cacheTime(20, rhub: 2, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string uri = $"{init.corsHost()}/kinopoisk/{kinopoisk_id}/iframe";
                if (!string.IsNullOrEmpty(orid))
                    uri = $"{init.corsHost()}/content/{orid}/iframe";

                string html = rch.enable ? await rch.Get(uri, httpHeaders(init)) : 
                                           await black_magic(uri, init, proxy);

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

                    PlaywrightBase.GotoAsync(page, host);
                    return await browser.WaitPageResult();
                }
            }
            catch { return null; }
        }
        #endregion
    }
}

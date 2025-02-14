using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using System.Text.RegularExpressions;
using Shared.Engine.Online;
using Shared.Engine;
using System.Collections.Generic;
using Shared.Model.Online.VDBmovies;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using System.Net;

namespace Lampac.Controllers.LITE
{
    public class VDBmovies : BaseOnlineController
    {
        static CookieContainer cookieContainer = new CookieContainer();

        [HttpGet]
        [Route("lite/vdbmovies")]
        async public Task<ActionResult> Index(string title, string original_title, long kinopoisk_id, string t, int sid, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = AppInit.conf.VDBmovies.Clone();
            if (!init.enable || init.rip || kinopoisk_id == 0)
                return OnError();

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            if (init.rhub && !AppInit.conf.rch.enable)
                return ShowError(RchClient.ErrorMsg);

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            var proxyManager = new ProxyManager("vdbmovies", init);
            var proxy = proxyManager.Get();

            var oninvk = new VDBmoviesInvoke
            (
               host,
               MaybeInHls(init.hls, init),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "vdbmovies")
            );

            var cache = await InvokeCache<EmbedModel>(rch.ipkey($"vdbmovies:{kinopoisk_id}", proxyManager), cacheTime(20, rhub: 2, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string uri = $"{init.corsHost()}/kinopoisk/{kinopoisk_id}/iframe";

                //string html = await black_magic(uri);
                string html = rch.enable ? await rch.Get(uri, httpHeaders(init)) : 
                                           await HttpClient.Get(uri, timeoutSeconds: 8, httpversion: 2, proxy: proxy, headers: httpHeaders(init), cookieContainer: cookieContainer);

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



        async ValueTask<string> black_magic(string uri)
        {
            try
            {
                using (var browser = await PuppeteerTo.Browser())
                {
                    if (browser == null)
                        return null;

                    var page = await browser.Page(new Dictionary<string, string>()
                    {
                        ["accept"] = "*/*",
                        ["cache-control"] = "no-cache",
                        ["dnt"] = "1",
                        ["origin"] = "https://kinogo.media",
                        ["pragma"] = "no-cache",
                        ["priority"] = "u=1, i",
                        ["referer"] = "https://kinogo.media/",
                        ["sec-ch-ua-mobile"] = "?0",
                        ["sec-ch-ua-platform"] = "\"Windows\"",
                        ["sec-fetch-dest"] = "empty",
                        ["sec-fetch-mode"] = "cors",
                        ["sec-fetch-site"] = "cross-site"
                    });

                    if (page == null)
                        return null;

                    var response = await page.GoToAsync(uri);
                    string html = await response.TextAsync();
                    if (html.Contains("<title>Just a moment...</title>") || html.Contains("<title>Attention Required! | Cloudflare</title>"))
                        return null;

                    if (!html.StartsWith("new Playerjs"))
                    {
                        await Task.Delay(400);
                        response = await page.GoToAsync($"view-source:{uri}");
                        html = await response.TextAsync();

                        if (!html.StartsWith("new Playerjs"))
                        {
                            await Task.Delay(200);
                            response = await page.GoToAsync($"view-source:{uri}");
                            html = await response.TextAsync();

                            if (!html.StartsWith("new Playerjs"))
                            {
                                response = await page.GoToAsync($"view-source:{uri}");
                                html = await response.TextAsync();
                            }
                        }
                    }

                    if (!html.Contains("new Playerjs"))
                        return null;

                    return html;
                }
            }
            catch { return null; }
        }
    }
}

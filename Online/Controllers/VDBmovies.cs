﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using System.Text.RegularExpressions;
using Shared.Engine.Online;
using Shared.Engine;
using Shared.Model.Online.VDBmovies;
using Microsoft.Playwright;
using System.Web;
using Lampac.Models.LITE;

namespace Lampac.Controllers.LITE
{
    public class VDBmovies : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/vdbmovies")]
        async public Task<ActionResult> Index(string title, string original_title, long kinopoisk_id, string t, int sid, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.VDBmovies);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (kinopoisk_id == 0 || Chromium.Status != ChromiumStatus.NoHeadless)
                return OnError();

            var oninvk = new VDBmoviesInvoke
            (
               host,
               MaybeInHls(init.hls, init),
               streamfile => HostStreamProxy(init, streamfile)
            );

            var cache = await InvokeCache<EmbedModel>($"vdbmovies:{kinopoisk_id}", cacheTime(20, rhub: 2, init: init), null, async res =>
            {
                string html = await black_magic($"{init.host}/kinopoisk/{kinopoisk_id}/iframe", init);

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

            return OnResult(cache, () => oninvk.Html(cache.Value, kinopoisk_id, title, original_title, t, s, sid, vast: init.vast, rjson: rjson), origsource: origsource);
        }



        async ValueTask<string> black_magic(string uri, OnlinesSettings init)
        {
            try
            {
                using (var browser = new Chromium())
                {
                    var page = await browser.NewPageAsync();
                    if (page == null)
                        return null;

                    await page.RouteAsync("**/*", async route =>
                    {
                        if (route.Request.Url.Contains("api/chromium/iframe"))
                        {
                            await route.ContinueAsync();
                            return;
                        }

                        if (route.Request.Url == uri)
                        {
                            string html = null;
                            await route.ContinueAsync(new RouteContinueOptions { Headers = httpHeaders(init).ToDictionary() });

                            var response = await page.WaitForResponseAsync(route.Request.Url);
                            if (response != null)
                                html = await response.TextAsync();

                            browser.completionSource.SetResult(html);
                            return;
                        }

                        await route.AbortAsync();
                    });

                    var response = await page.GotoAsync(Chromium.IframeUrl(uri));
                    if (response == null)
                        return null;

                    return await browser.WaitPageResult();
                }
            }
            catch { return null; }
        }
    }
}

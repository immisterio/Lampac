using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using System.Text.RegularExpressions;
using Shared.Engine.Online;
using Shared.Engine;
using System.Collections.Generic;
using Shared.Model.Online.VDBmovies;
using Lampac.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class VDBmovies : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/vdbmovies")]
        async public Task<ActionResult> Index(string title, string original_title, long kinopoisk_id, string t, int sid, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = AppInit.conf.VDBmovies.Clone();

            if (!init.enable || kinopoisk_id == 0)
                return OnError();

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            if (init.rhub)
                return ShowError(RchClient.ErrorMsg);

            var oninvk = new VDBmoviesInvoke
            (
               host,
               MaybeInHls(init.hls, init),
               streamfile => HostStreamProxy(init, streamfile, plugin: "vdbmovies")
            );

            var cache = await InvokeCache<EmbedModel>($"vdbmovies:{kinopoisk_id}", cacheTime(20, init: init), async res =>
            {
                string html = await black_magic($"{init.corsHost()}/kinopoisk/{kinopoisk_id}/iframe");
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

            return OnResult(cache, () => oninvk.Html(cache.Value, kinopoisk_id, title, original_title, t, s, sid, rjson: rjson), origsource: origsource);
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
                        ["origin"] = "https://spider-man-lordfilm.cam",
                        ["pragma"] = "no-cache",
                        ["priority"] = "u=1, i",
                        ["referer"] = "https://spider-man-lordfilm.cam/",
                        ["sec-ch-ua"] = "\"Google Chrome\";v=\"129\", \"Not = A ? Brand\";v=\"8\", \"Chromium\";v=\"129\"",
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

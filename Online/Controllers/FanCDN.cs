using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using Shared.Model.Online.FanCDN;
using Shared.Engine.Online;
using Shared.Engine;
using Lampac.Models.LITE;
using PuppeteerSharp;

namespace Lampac.Controllers.LITE
{
    public class FanCDN : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/fancdn")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int year, int t = -1, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.FanCDN);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var oninvk = new FanCDNInvoke
            (
               host,
               init.corsHost(),
               ongettourl => black_magic(ongettourl, init),
               streamfile => HostStreamProxy(init, streamfile)
            );

            var cache = await InvokeCache<EmbedModel>($"fancdn:{kinopoisk_id}:{imdb_id}", cacheTime(20, init: init), null, async res =>
            {
                return await oninvk.Embed(null, imdb_id, kinopoisk_id);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, imdb_id, kinopoisk_id, title, original_title, t, s, rjson: rjson, vast: init.vast), origsource: origsource);
        }


        async ValueTask<string> black_magic(string uri, OnlinesSettings init)
        {
            using (var browser = await PuppeteerTo.Browser())
            {
                if (browser == null)
                    return null;

                var page = await browser.Page(httpHeaders(init).ToDictionary());
                if (page == null)
                    return null;

                await page.DeleteCookieAsync(new CookieParam() { Domain = ".fancdn.net",  Name = "cf_clearance" });

                var response = await page.GoToAsync($"view-source:{uri}");
                return await response.TextAsync();
            }
        }
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using Shared.Model.Online;
using System.Text.RegularExpressions;
using Shared.Model.Online.VDBmovies;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class VDBmovies : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/vdbmovies")]
        async public Task<ActionResult> Index(string title, string original_title, long kinopoisk_id, string t, int sid, int s = -1)
        {
            var init = AppInit.conf.VDBmovies.Clone();

            if (!init.enable || kinopoisk_id == 0)
                return OnError();

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            reset: var rch = new RchClient(HttpContext, host, init.rhub);
            var proxyManager = new ProxyManager("vdbmovies", init);
            var proxy = proxyManager.Get();

            var oninvk = new VDBmoviesInvoke
            (
               host,
               MaybeInHls(init.hls, init),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "vdbmovies")
            );

            var cache = await InvokeCache<EmbedModel>(rch.ipkey($"vdbmovies:{kinopoisk_id}", proxyManager), cacheTime(20, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string uri = $"{init.corsHost()}/kinopoisk/{kinopoisk_id}/iframe";

                /////////
                // https://spider-man-lordfilm.cam/
                // https://torrent-film.online
                // http://kinozadrot.lol
                /////////
                string html = init.rhub ? await rch.Get(uri) : await HttpClient.Get(uri, proxy: proxy, httpversion: 2, headers: HeadersModel.Init(
                    ("accept", "*/*"),
                    ("cache-control", "no-cache"),
                    ("dnt", "1"),
                    ("origin", "https://torrent-film.online"),
                    ("pragma", "no-cache"),
                    ("priority", "u=1, i"),
                    ("referer", "https://torrent-film.online/"),
                    ("sec-ch-ua", "\"Google Chrome\";v=\"129\", \"Not = A ? Brand\";v=\"8\", \"Chromium\";v=\"129\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "cross-site")
                ));

                if (html == null)
                {
                    proxyManager.Refresh();
                    return null;
                }

                string file = Regex.Match(html, "file:([\t ]+)?'(#[^']+)").Groups[2].Value;
                if (string.IsNullOrEmpty(file)) 
                    return null;

                return oninvk.Embed(oninvk.DecodeEval(file));

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
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, kinopoisk_id, title, original_title, t, s, sid));
        }
    }
}

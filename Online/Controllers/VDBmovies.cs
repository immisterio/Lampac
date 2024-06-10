using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using Shared.Model.Online;
using System.Text.RegularExpressions;
using Shared.Model.Online.VDBmovies;
using Lampac.Engine.CORE;
using Shared.Engine;
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
            var init = AppInit.conf.VDBmovies;

            if (!init.enable || kinopoisk_id == 0)
                return OnError();

            var rch = new RchClient(HttpContext, host, init.rhub);
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

                string html = init.rhub ? await rch.Get(uri) : await HttpClient.Get(uri, proxy: proxy, headers: HeadersModel.Init(
                    ("Accept-Language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                    ("Cache-Control", "no-cache"),
                    ("Dnt", "1"),
                    ("Origin", "https://cdnmovies.net"),
                    ("Referer", "https://cdnmovies.net/"),
                    ("Pragma", "no-cache"),
                    ("Priority", "u=1, i"),
                    ("Sec-Ch-Ua", "\"Google Chrome\";v=\"125\", \"Chromium\";v=\"125\", \"Not.A/Brand\";v=\"24\""),
                    ("Sec-Ch-Ua-Mobile", "?0"),
                    ("Sec-Ch-Ua-Platform", "\"Windows\""),
                    ("Sec-Fetch-Dest", "empty"),
                    ("Sec-Fetch-Mode", "cors"),
                    ("Sec-Fetch-Site", "cross-site"),
                    ("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36")
                ));

                if (html == null)
                {
                    proxyManager.Refresh();
                    return null;
                }

                string file = Regex.Match(html, "file: ?'(#[^&']+)").Groups[1].Value;
                if (string.IsNullOrEmpty(file)) 
                    return null;

                try
                {
                    using (var browser = await PuppeteerTo.Browser())
                    {
                        var page = await browser.MainPage();

                        if (page == null)
                            return null;

                        return oninvk.Embed(await page.EvaluateExpressionAsync<string>(oninvk.EvalCode(file)));
                    }
                }
                catch
                {
                    return null;
                }
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, kinopoisk_id, title, original_title, t, s, sid));
        }
    }
}

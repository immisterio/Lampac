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
                    ("Origin", "https://cdnmovies.net"),
                    ("Referer", "https://cdnmovies.net/")
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

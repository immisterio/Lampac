using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using Online;
using Shared.Engine.Online;
using Shared.Model.Online.Rezka;
using System;
using Shared.Model.Online;

namespace Lampac.Controllers.LITE
{
    public class Rezka : BaseOnlineController
    {
        #region InitRezkaInvoke
        static DateTimeOffset _ym = DateTimeOffset.UtcNow;

        static string cookie_default = $"PHPSESSID={CrypTo.unic(26).ToLower()}; dle_user_taken=1; dle_user_token={CrypTo.md5(DateTime.Now.ToString())}; _ym_uid={_ym.ToUnixTimeMilliseconds() + CrypTo.unic(5, true)}; _ym_d={_ym.ToUnixTimeSeconds()}; _ym_isad=2; _ym_visorc=b";

        public RezkaInvoke InitRezkaInvoke()
        {
            var init = AppInit.conf.Rezka;

            var proxyManager = new ProxyManager("rezka", init);
            var proxy = proxyManager.Get();

            var headers = HeadersModel.Init(
                ("Cookie", string.IsNullOrEmpty(init.cookie) ? cookie_default : init.cookie),
                ("Origin", init.host),
                ("Referer", init.host + "/")
            );

            if (init.xapp)
                headers.Add(new HeadersModel("X-App-Hdrezka-App", "1"));

            if (init.xrealip)
                headers.Add(new HeadersModel("realip", HttpContext.Connection.RemoteIpAddress.ToString()));

            string country = init.forceua ? "UA" : GeoIP2.Country(HttpContext.Connection.RemoteIpAddress.ToString());

            return new RezkaInvoke
            (
                host,
                init.corsHost(),
                init.hls,
                ongettourl => HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, addHeaders: headers),
                (url, data) => HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, addHeaders: headers),
                streamfile => HostStreamProxy(init, RezkaInvoke.fixcdn(country, init.uacdn, streamfile), proxy: proxy, plugin: "rezka")
            );
        }
        #endregion

        [HttpGet]
        [Route("lite/rezka")]
        async public Task<ActionResult> Index(long kinopoisk_id, string imdb_id, string title, string original_title, int clarification, int year, int s = -1, string href = null)
        {
            if (!AppInit.conf.Rezka.enable)
                return OnError();

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
                return OnError();

            var oninvk = InitRezkaInvoke();
            var proxyManager = new ProxyManager("rezka", AppInit.conf.Rezka);

            var content = await InvokeCache($"rezka:{kinopoisk_id}:{imdb_id}:{title}:{original_title}:{year}:{clarification}:{href}", cacheTime(20), () => oninvk.Embed(kinopoisk_id, imdb_id, title, original_title, clarification, year, href));
            if (content == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(content, kinopoisk_id, imdb_id, title, original_title, clarification, year, s, href, true), "text/html; charset=utf-8");
        }


        #region Serial
        [HttpGet]
        [Route("lite/rezka/serial")]
        async public Task<ActionResult> Serial(long kinopoisk_id, string imdb_id, string title, string original_title, int clarification,int year, string href, long id, int t, int s = -1)
        {
            if (!AppInit.conf.Rezka.enable)
                return OnError();

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
                return OnError();

            var oninvk = InitRezkaInvoke();
            var proxyManager = new ProxyManager("rezka", AppInit.conf.Rezka);

            Episodes root = await InvokeCache($"rezka:view:serial:{id}:{t}", cacheTime(20), () => oninvk.SerialEmbed(id, t));
            if (root == null)
                return OnError(proxyManager);

            var content = await InvokeCache($"rezka:{kinopoisk_id}:{imdb_id}:{title}:{original_title}:{year}:{clarification}:{href}", cacheTime(20), () => oninvk.Embed(kinopoisk_id, imdb_id, title, original_title, clarification, year, href));
            if (content == null)
                return OnError(proxyManager);

            return Content(oninvk.Serial(root, content, kinopoisk_id, imdb_id, title, original_title, clarification, year, href, id, t, s, true), "text/html; charset=utf-8");
        }
        #endregion

        #region Movie
        [HttpGet]
        [Route("lite/rezka/movie")]
        async public Task<ActionResult> Movie(string title, string original_title, long id, int t, int director = 0, int s = -1, int e = -1, string favs = null, bool play = false)
        {
            var init = AppInit.conf.Rezka;
            if (!init.enable)
                return OnError();

            var oninvk = InitRezkaInvoke();
            var proxyManager = new ProxyManager("rezka", init);

            string realip = (init.xrealip && init.corseu) ? HttpContext.Connection.RemoteIpAddress.ToString() : "";

            var md = await InvokeCache($"rezka:view:get_cdn_series:{id}:{t}:{director}:{s}:{e}:{realip}", cacheTime(20, mikrotik: 1), () => oninvk.Movie(id, t, director, s, e, favs), proxyManager);
            if (md == null)
                return OnError(proxyManager);

            string result = oninvk.Movie(md, title, original_title, play);
            if (result == null)
                return OnError();

            if (play)
                return Redirect(result);

            return Content(result, "application/json; charset=utf-8");
        }
        #endregion
    }
}

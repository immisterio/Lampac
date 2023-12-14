using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using Online;
using Shared.Engine.Online;
using Shared.Model.Online.Rezka;

namespace Lampac.Controllers.LITE
{
    public class Rezka : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("rezka", AppInit.conf.Rezka);

        #region InitRezkaInvoke
        public RezkaInvoke InitRezkaInvoke()
        {
            var proxy = proxyManager.Get();
            var init = AppInit.conf.Rezka;

            var headers = new List<(string name, string val)>();

            if (!string.IsNullOrEmpty(init.cookie))
                headers.Add(("Cookie", init.cookie));

            if (init.xapp)
                headers.Add(("X-App-Hdrezka-App", "1"));

            if (init.xrealip)
                headers.Add(("X-Real-IP", HttpContext.Connection.RemoteIpAddress.ToString()));

            headers.Add(("Origin", init.host));
            headers.Add(("Referer", init.host + "/"));

            return new RezkaInvoke
            (
                host,
                init.corsHost(),
                ongettourl => HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, addHeaders: headers),
                (url, data) => HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, addHeaders: headers),
                streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "rezka")
            );
        }
        #endregion

        [HttpGet]
        [Route("lite/rezka")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, string original_language, int year, int s = -1, string href = null)
        {
            if (!AppInit.conf.Rezka.enable)
                return OnError();

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
                return OnError();

            if (original_language != "en")
                clarification = 1;

            var oninvk = InitRezkaInvoke();

            var content = await InvokeCache($"rezka:view:{title}:{original_title}:{year}:{clarification}:{href}", cacheTime(20), () => oninvk.Embed(title, original_title, clarification, year, href));
            if (content == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(content, title, original_title, clarification, year, s, href, true), "text/html; charset=utf-8");
        }


        #region Serial
        [HttpGet]
        [Route("lite/rezka/serial")]
        async public Task<ActionResult> Serial(string title, string original_title, int clarification, int year, string href, long id, int t, int s = -1)
        {
            if (!AppInit.conf.Rezka.enable)
                return OnError();

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
                return OnError();

            var oninvk = InitRezkaInvoke();

            Episodes root = await InvokeCache($"rezka:view:serial:{id}:{t}", cacheTime(20), () => oninvk.SerialEmbed(id, t));
            if (root == null)
                return OnError(proxyManager);

            var content = await InvokeCache($"rezka:view:{title}:{original_title}:{year}:{clarification}:{href}", cacheTime(20), () => oninvk.Embed(title, original_title, clarification, year, href));
            if (content == null)
                return OnError(proxyManager);

            return Content(oninvk.Serial(root, content, title, original_title, clarification, year, href, id, t, s, true), "text/html; charset=utf-8");
        }
        #endregion

        #region Movie
        [HttpGet]
        [Route("lite/rezka/movie")]
        async public Task<ActionResult> Movie(string title, string original_title, long id, int t, int director = 0, int s = -1, int e = -1, string favs = null, bool play = false)
        {
            if (!AppInit.conf.Rezka.enable)
                return OnError();

            var oninvk = InitRezkaInvoke();

            var md = await InvokeCache($"rezka:view:get_cdn_series:{id}:{t}:{director}:{s}:{e}", cacheTime(20), () => oninvk.Movie(id, t, director, s, e, favs));
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

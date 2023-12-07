using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using System.Collections.Generic;

namespace Lampac.Controllers.LITE
{
    public class Voidboost : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("voidboost", AppInit.conf.Voidboost);

        #region InitVoidboostInvoke
        public VoidboostInvoke InitVoidboostInvoke()
        {
            var proxy = proxyManager.Get();

            var headers = new List<(string name, string val)>();

            if (AppInit.conf.Voidboost.xrealip)
            {
                headers.Add(("X-Real-IP", HttpContext.Connection.RemoteIpAddress.ToString()));
                headers.Add(("Origin", "http://baskino.me"));
                headers.Add(("Referer", "http://baskino.me/"));
            }

            return new VoidboostInvoke
            (
                host,
                AppInit.conf.Voidboost.corsHost(),
                ongettourl => HttpClient.Get(AppInit.conf.Voidboost.corsHost(ongettourl), timeoutSeconds: 8, proxy: proxy, addHeaders: headers),
                (url, data) => HttpClient.Post(AppInit.conf.Voidboost.corsHost(url), data, timeoutSeconds: 8, proxy: proxy, addHeaders: headers),
                streamfile => HostStreamProxy(AppInit.conf.Voidboost, streamfile, proxy: proxy, plugin: "voidboost")
            );
        }
        #endregion

        [HttpGet]
        [Route("lite/voidboost")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, string t)
        {
            if (!AppInit.conf.Voidboost.enable)
                return OnError();

            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return OnError();

            var oninvk = InitVoidboostInvoke();

            var content = await InvokeCache($"voidboost:view:{kinopoisk_id}:{imdb_id}:{t}:{proxyManager.CurrentProxyIp}", AppInit.conf.multiaccess ? 20 : 10, () => oninvk.Embed(imdb_id, kinopoisk_id, t));
            if (content == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(content, imdb_id, kinopoisk_id, title, original_title, t), "text/html; charset=utf-8");
        }


        #region Serial
        [HttpGet]
        [Route("lite/voidboost/serial")]
        async public Task<ActionResult> Serial(string imdb_id, long kinopoisk_id, string title, string original_title, string t, int s)
        {
            if (!AppInit.conf.Voidboost.enable || string.IsNullOrEmpty(t))
                return Content(string.Empty);

            var oninvk = InitVoidboostInvoke();

            string html = await InvokeCache($"voidboost:view:serial:{t}:{s}:{proxyManager.CurrentProxyIp}", AppInit.conf.multiaccess ? 20 : 10, () => oninvk.Serial(imdb_id, kinopoisk_id, title, original_title, t, s, true));
            if (html == null)
                return OnError(proxyManager);

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        #region Movie / Episode
        [HttpGet]
        [Route("lite/voidboost/movie")]
        [Route("lite/voidboost/episode")]
        async public Task<ActionResult> Movie(string title, string original_title, string t, int s, int e, bool play)
        {
            if (!AppInit.conf.Voidboost.enable)
                return OnError();

            var oninvk = InitVoidboostInvoke();

            var md = await InvokeCache($"rezka:view:stream:{t}:{s}:{e}:{proxyManager.CurrentProxyIp}:{play}", AppInit.conf.multiaccess ? 20 : 10, () => oninvk.Movie(t, s, e));
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

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;

namespace Lampac.Controllers.LITE
{
    public class iRemux : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("remux", AppInit.conf.iRemux);

        #region iRemuxInvoke
        public iRemuxInvoke InitRemuxInvoke()
        {
            var proxy = proxyManager.Get();
            var init = AppInit.conf.iRemux;

            return new iRemuxInvoke
            (
               host,
               init.corsHost(),
               ongettourl => HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, cookie: init.cookie, headers: httpHeaders(init)),
               (url, data) => HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, cookie: init.cookie, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "remux"),
               requesterror: () => proxyManager.Refresh()
            );
        }
        #endregion

        [HttpGet]
        [Route("lite/remux")]
        async public Task<ActionResult> Index(string title, string original_title, int year, string href)
        {
            var init = AppInit.conf.iRemux;

            if (!init.enable)
                return OnError();

            if (string.IsNullOrWhiteSpace(title ?? original_title) || year == 0)
                return OnError();

            var oninvk = InitRemuxInvoke();

            var content = await InvokeCache($"remux:{title}:{original_title}:{year}:{href}", cacheTime(40, init: init), () => oninvk.Embed(title, original_title, year, href), proxyManager);
            if (content == null)
                return OnError();

            return Content(oninvk.Html(content, title, original_title, year), "text/html; charset=utf-8");
        }


        [HttpGet]
        [Route("lite/remux/movie")]
        async public Task<ActionResult> Movie(string linkid, string quality, string title, string original_title)
        {
            if (!AppInit.conf.iRemux.enable)
                return OnError();

            var oninvk = InitRemuxInvoke();

            string weblink = await InvokeCache($"remux:view:{linkid}:{proxyManager.CurrentProxyIp}", cacheTime(20), () => oninvk.Weblink(linkid), proxyManager);
            if (weblink == null)
                return OnError();

            return Content(oninvk.Movie(weblink, quality, title, original_title), "application/json; charset=utf-8");
        }
    }
}

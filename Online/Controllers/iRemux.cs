using Microsoft.AspNetCore.Mvc;

namespace Online.Controllers
{
    public class iRemux : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager(AppInit.conf.iRemux);

        #region iRemuxInvoke
        public iRemuxInvoke InitRemuxInvoke()
        {
            var proxy = proxyManager.Get();
            var init = AppInit.conf.iRemux;

            return new iRemuxInvoke
            (
               host,
               init.corsHost(),
               ongettourl => Http.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, cookie: init.cookie, headers: httpHeaders(init)),
               (url, data) => Http.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, cookie: init.cookie, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy),
               requesterror: () => proxyManager.Refresh()
            );
        }
        #endregion

        [HttpGet]
        [Route("lite/remux")]
        async public ValueTask<ActionResult> Index(string title, string original_title, int year, string href, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.iRemux);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrWhiteSpace(title ?? original_title) || year == 0)
                return OnError();

            var oninvk = InitRemuxInvoke();

            var content = await InvokeCache($"remux:{title}:{original_title}:{year}:{href}", cacheTime(40, init: init), () => oninvk.Embed(title, original_title, year, href), proxyManager);
            if (content == null)
                return OnError();

            return ContentTo(oninvk.Html(content, title, original_title, year, rjson: rjson));
        }


        [HttpGet]
        [Route("lite/remux/movie")]
        async public ValueTask<ActionResult> Movie(string linkid, string quality, string title, string original_title)
        {
            var init = await loadKit(AppInit.conf.iRemux);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var oninvk = InitRemuxInvoke();

            string weblink = await InvokeCache($"remux:view:{linkid}:{proxyManager.CurrentProxyIp}", cacheTime(20), () => oninvk.Weblink(linkid), proxyManager);
            if (weblink == null)
                return OnError();

            return ContentTo(oninvk.Movie(weblink, quality, title, original_title, vast: init.vast));
        }
    }
}

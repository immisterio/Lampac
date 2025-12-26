using Microsoft.AspNetCore.Mvc;

namespace Online.Controllers
{
    public class iRemux : BaseOnlineController
    {
        iRemuxInvoke oninvk;

        public iRemux() : base(AppInit.conf.iRemux) 
        {
            requestInitialization = () =>
            {
                oninvk = new iRemuxInvoke
                (
                   host,
                   init.corsHost(),
                   ongettourl => httpHydra.Get(ongettourl, addheaders: HeadersModel.Init("cookie", init.cookie)),
                   streamfile => streamfile,
                   requesterror: () => proxyManager.Refresh()
                );
            };
        }

        [HttpGet]
        [Route("lite/remux")]
        async public ValueTask<ActionResult> Index(string title, string original_title, int year, string href, bool rjson = false)
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title) || year == 0)
                return OnError();

            if (await IsRequestBlocked(rch: false))
                return badInitMsg;

            var content = await InvokeCache($"remux:{title}:{original_title}:{year}:{href}", 40, 
                () => oninvk.Embed(title, original_title, year, href)
            );

            if (content == null)
                return OnError();

            return ContentTo(oninvk.Tpl(content, title, original_title, year));
        }


        [HttpGet]
        [Route("lite/remux/movie")]
        async public ValueTask<ActionResult> Movie(string linkid, string quality, string title, string original_title)
        {
            if (await IsRequestBlocked(rch: false))
                return badInitMsg;

            string weblink = await InvokeCache($"remux:view:{linkid}:{proxyManager.CurrentProxyIp}", 20, () => oninvk.Weblink(linkid));
            if (weblink == null)
                return OnError();

            return ContentTo(oninvk.Movie(weblink, quality, title, original_title, vast: init.vast));
        }
    }
}

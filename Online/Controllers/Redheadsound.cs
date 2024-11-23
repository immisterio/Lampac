using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Online.Redheadsound;

namespace Lampac.Controllers.LITE
{
    public class Redheadsound : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/redheadsound")]
        async public Task<ActionResult> Index(string rchtype, string title, string original_title, int year, int clarification, bool origsource = false, bool rjson = false)
        {
            var init = AppInit.conf.Redheadsound.Clone();

            if (!init.enable)
                return OnError();

            if (init.rhub && !AppInit.conf.rch.enable)
                return ShowError(RchClient.ErrorMsg);

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            if (string.IsNullOrWhiteSpace(title) || year == 0)
                return OnError();

            reset: var rch = new RchClient(HttpContext, host, init.rhub);
            var proxyManager = new ProxyManager("redheadsound", init);
            var proxy = proxyManager.Get();

            var oninvk = new RedheadsoundInvoke
            (
               host,
               init.corsHost(),
               ongettourl => init.rhub ? rch.Get(init.cors(ongettourl)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               (url, data) => init.rhub ? rch.Post(init.cors(url), data) : HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "redheadsound"),
               requesterror: () => proxyManager.Refresh()
            );

            var cache = await InvokeCache<EmbedModel>($"redheadsound:view:{title}:{year}:{clarification}", cacheTime(30, init: init), proxyManager, async res =>
            {
                if (rch.IsNotSupport(rchtype, "web", out string rch_error))
                    return ShowError(rch_error);

                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(clarification == 1 ? title : (original_title ?? title), year);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, title, rjson: rjson), origsource: origsource);
        }
    }
}

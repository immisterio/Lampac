using Microsoft.AspNetCore.Mvc;

namespace Online.Controllers
{
    public class Redheadsound : BaseOnlineController
    {
        public Redheadsound() : base(AppInit.conf.Redheadsound) { }

        [HttpGet]
        [Route("lite/redheadsound")]
        async public ValueTask<ActionResult> Index(string title, string original_title, int year, int clarification, bool rjson = false)
        {
            if (string.IsNullOrWhiteSpace(title) || year == 0)
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            var oninvk = new RedheadsoundInvoke
            (
               host,
               init.corsHost(),
               ongettourl => httpHydra.Get(ongettourl),
               (url, data) => httpHydra.Post(url, data),
               streamfile => HostStreamProxy(streamfile),
               requesterror: () => proxyManager.Refresh(rch)
            );

            rhubFallback:
            var cache = await InvokeCacheResult($"redheadsound:view:{title}:{year}", 30, 
                () => oninvk.Embed(title, year)
            );

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return OnResult(cache, () => oninvk.Tpl(cache.Value, title, vast: init.vast, rjson: rjson));
        }
    }
}

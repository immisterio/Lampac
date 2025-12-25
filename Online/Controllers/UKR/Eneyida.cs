using Microsoft.AspNetCore.Mvc;
using Shared.Models.Online.Eneyida;

namespace Online.Controllers
{
    public class Eneyida : BaseOnlineController
    {
        public Eneyida() : base(AppInit.conf.Eneyida) { }

        [HttpGet]
        [Route("lite/eneyida")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, int t = -1, int s = -1, string href = null, bool rjson = false, bool similar = false, string source = null, string id = null)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.ToLower() == "eneyida")
                    href = $"{init.host}/{id}";
            }

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(original_title) || year == 0))
                return OnError();

            var oninvk = new EneyidaInvoke
            (
               host,
               init.corsHost(),
               ongettourl => rch.enable 
                    ? rch.Get(init.cors(ongettourl), httpHeaders(init)) 
                    : Http.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               (url, data) => rch.enable 
                    ? rch.Post(init.cors(url), data, httpHeaders(init)) 
                    : Http.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               onstreamtofile => HostStreamProxy(onstreamtofile),
               requesterror: () => proxyManager.Refresh(rch)
            );

            reset:
            var cache = await InvokeCacheResult($"eneyida:view:{title}:{year}:{href}:{clarification}:{similar}", 40, 
                () => oninvk.Embed((similar || clarification == 1) ? title : original_title, year, href, similar)
            );

            if (IsRhubFallback(cache))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, clarification, title, original_title, year, t, s, href, vast: init.vast, rjson: rjson));
        }
    }
}

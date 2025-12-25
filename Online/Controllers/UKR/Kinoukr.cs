using Microsoft.AspNetCore.Mvc;

namespace Online.Controllers
{
    public class Kinoukr : BaseOnlineController
    {
        public Kinoukr() : base(AppInit.conf.Kinoukr) { }

        [HttpGet]
        [Route("lite/kinoukr")]
        async public ValueTask<ActionResult> Index(string title, string original_title, int clarification, int year, string t, int s = -1, string href = null, bool rjson = false, string source = null, string id = null)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            var oninvk = new KinoukrInvoke
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

            if (string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.ToLower() == "kinoukr")
                    href = await InvokeCache($"kinoukr:source:{id}", 180, () => oninvk.getIframeSource($"{init.host}/{id}"));
            }

            reset:
            var cache = await InvokeCacheResult($"kinoukr:view:{title}:{original_title}:{year}:{href}:{clarification}", 40, 
                () => oninvk.EmbedKurwa(clarification, title, original_title, year, href)
            );

            if (IsRhubFallback(cache))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, clarification, title, original_title, year, t, s, href, vast: init.vast, rjson: rjson));
        }
    }
}

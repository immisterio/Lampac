using Microsoft.AspNetCore.Mvc;
using Shared.Models.Online.AniLibria;

namespace Online.Controllers
{
    public class AniLibriaOnline : BaseOnlineController
    {
        public AniLibriaOnline() : base(AppInit.conf.AnilibriaOnline) { }

        [HttpGet]
        [Route("lite/anilibria")]
        async public ValueTask<ActionResult> Index(string title, string code, int year, bool rjson = false, bool similar = false)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(title))
                return OnError();

            var oninvk = new AniLibriaInvoke
            (
               host,
               init.corsHost(),
               ongettourl => rch.enable 
                    ? rch.Get<List<RootObject>>(init.cors(ongettourl), httpHeaders(init)) 
                    : Http.Get<List<RootObject>>(init.cors(ongettourl), timeoutSeconds: 40, proxy: proxy, IgnoreDeserializeObject: true, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(streamfile),
               requesterror: () => proxyManager.Refresh(rch)
            );

            reset:
            var cache = await InvokeCacheResult($"anilibriaonline:{title}", 40, 
                () => oninvk.Embed(title)
            );

            if (IsRhubFallback(cache))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, title, code, year, vast: init.vast, rjson: rjson, similar: similar));
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Shared.Models.Online.AniLibria;

namespace Online.Controllers
{
    public class AniLibriaOnline : BaseOnlineController
    {
        public AniLibriaOnline() : base(AppInit.conf.AnilibriaOnline) { }

        [HttpGet]
        [Route("lite/anilibria")]
        async public Task<ActionResult> Index(string title, string code, int year, bool rjson = false, bool similar = false)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(title))
                return OnError();

            var oninvk = new AniLibriaInvoke
            (
               host,
               init.corsHost(),
               ongettourl => httpHydra.Get<List<RootObject>>(ongettourl, IgnoreDeserializeObject: true),
               streamfile => HostStreamProxy(streamfile),
               requesterror: () => proxyManager?.Refresh()
            );

            rhubFallback:
            var cache = await InvokeCacheResult($"anilibriaonline:{title}", 40, 
                () => oninvk.Embed(title)
            );

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return await ContentTpl(cache, () => oninvk.Tpl(cache.Value, title, code, year, vast: init.vast, rjson: rjson, similar: similar));
        }
    }
}

using Shared;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Attributes;

namespace AniLibria;

public class AniLibriaController : BaseOnlineController
{
    public AniLibriaController() : base(ModInit.conf) { }

    [HttpGet, Staticache(manually: true)]
    [Route("lite/anilibria")]
    public async Task<ActionResult> Index(string title, string code, short year, bool rjson = false, bool similar = false)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        if (string.IsNullOrEmpty(title))
            return OnError();

        var oninvk = new AniLibriaInvoke
        (
           host,
           init.host,
           ongettourl => httpHydra.Get<List<RootObject>>(ongettourl, IgnoreDeserializeObject: true),
           streamfile => HostStreamProxy(streamfile)
        );

    rhubFallback:
        var cache = await InvokeCacheResult($"anilibriaonline:{title}", 40,
            () => oninvk.Embed(title)
        );

        if (IsRhubFallback(cache))
            goto rhubFallback;

        return ContentTpl(cache, () => oninvk.Tpl(cache.Value, title, code, year, vast: init.vast, rjson: rjson, similar: similar));
    }
}

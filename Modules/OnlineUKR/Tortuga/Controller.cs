using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using System.Threading.Tasks;

namespace Tortuga;

public class TortugaController : BaseOnlineController
{
    public TortugaController() : base(ModInit.conf) { }

    [HttpGet, Staticache(manually: true)]
    [Route("lite/tortuga")]
    async public Task<ActionResult> Index(string uri, string title, string original_title, string t, short s = -1, bool rjson = false)
    {
        string href = DecryptQuery(uri);

        if (string.IsNullOrWhiteSpace(href))
            return OnError("href");

        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        var oninvk = new TortugaInvoke
        (
           host,
           httpHydra,
           streamfile => HostStreamProxy(streamfile)
        );

    rhubFallback:

        var cache = await InvokeCacheResult($"tortuga:view:{href}", 180,
            () => oninvk.Embed(href)
        );

        if (IsRhubFallback(cache))
            goto rhubFallback;

        return ContentTpl(cache,
            () => oninvk.Tpl(cache.Value, title, original_title, t, s, uri, vast: init.vast, rjson: rjson)
        );
    }
}

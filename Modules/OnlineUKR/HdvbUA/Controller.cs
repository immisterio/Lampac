using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using System.Threading.Tasks;

namespace HdvbUA;

public class HdvbUAController : BaseOnlineController
{
    public HdvbUAController() : base(ModInit.conf) { }

    [HttpGet, Staticache(manually: true)]
    [Route("lite/hdvbua")]
    async public Task<ActionResult> Index(string uri, string title, string original_title, short t = -1, short s = -1, bool rjson = false)
    {
        string href = DecryptQuery(uri);

        if (string.IsNullOrWhiteSpace(href))
            return OnError("href");

        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        var oninvk = new HdvbUAInvoke
        (
           host,
           httpHydra,
           onstreamtofile => HostStreamProxy(onstreamtofile)
        );

    rhubFallback:

        var cache = await InvokeCacheResult($"hdvbua:view:{href}", 40,
            () => oninvk.Embed(href),
            textJson: true
        );

        if (IsRhubFallback(cache))
            goto rhubFallback;

        return ContentTpl(cache,
            () => oninvk.Tpl(cache.Value, title, original_title, t, s, uri, vast: init.vast, rjson: rjson)
        );
    }
}

using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.Templates;
using Shared.Services.RxEnumerate;
using System.Threading.Tasks;

namespace Ashdi;

public class AshdiController : BaseOnlineController
{
    public AshdiController() : base(ModInit.conf) { }

    [HttpGet]
    [Staticache]
    [Route("lite/ashdi")]
    async public Task<ActionResult> Index(string uri, string title, string original_title, int t = -1, int s = -1, bool rjson = false)
    {
        string href = DecryptQuery(uri);

        if (string.IsNullOrWhiteSpace(href))
            return OnError("href");

        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        var oninvk = new AshdiInvoke
        (
           host,
           httpHydra,
           streamfile => HostStreamProxy(streamfile)
        );

    rhubFallback:

        var cache = await InvokeCacheResult($"ashdi:view:{href}", 180,
            () => oninvk.Embed(href),
            textJson: true
        );

        if (IsRhubFallback(cache))
            goto rhubFallback;

        return ContentTpl(cache,
            () => oninvk.Tpl(cache.Value, uri, title, original_title, t, s, vast: init.vast, rjson: rjson)
        );
    }

    [HttpGet]
    [Route("lite/ashdi/vod.m3u8")]
    async public Task<ActionResult> Vod(string uri, string title, bool play)
    {
        uri = DecryptQuery(uri);

        if (string.IsNullOrWhiteSpace(uri))
            return OnError("uri");

        if (await IsRequestBlocked(rch: true, rch_check: false))
            return badInitMsg;

        if (rch != null)
        {
            if (rch.IsNotConnected())
            {
                if (init.rhub_fallback && play)
                    rch.Disabled();
                else
                    return ContentTo(rch.connectionMsg);
            }

            if (!play && rch.IsRequiredConnected())
                return ContentTo(rch.connectionMsg);

            if (rch.IsNotSupport(out string rch_error))
                return ShowError(rch_error);
        }

    rhubFallback:

        var cache = await InvokeCacheResult<string>($"ashdi:video:{uri}", 30, async e =>
        {
            string hls = null;

            await httpHydra.GetSpan(uri, spanAction: iframe =>
            {
                hls = Rx.Match(iframe, "file:'(https?://[^\"']+\\.m3u8)'");
            });

            if (string.IsNullOrEmpty(hls))
                return e.Fail("hls", refresh_proxy: true);

            return e.Success(hls);
        });

        if (IsRhubFallback(cache))
            goto rhubFallback;

        if (!cache.IsSuccess)
            return OnError(cache.ErrorMsg);

        string link = HostStreamProxy(cache.Value);

        if (play)
            return RedirectToPlay(link);

        return ContentTo(VideoTpl.ToJson(
            "play",
            link,
            title,
            vast: init.vast,
            httpContext: HttpContext
        ));
    }
}

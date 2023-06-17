using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class BongaCamsController : BaseController
    {
        [JSInvokable("bgs")]
        async public static ValueTask<dynamic> Index(string args)
        {
            string? sort = parse_arg("sort", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            string? html = await BongaCamsTo.InvokeHtml(AppInit.BongaCams.corsHost(), sort, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return OnResult(BongaCamsTo.Playlist(html), BongaCamsTo.Menu(null, sort));
        }
    }
}

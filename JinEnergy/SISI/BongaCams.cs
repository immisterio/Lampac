using JinEnergy.Engine;
using JinEnergy.Model;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class BongaCamsController : BaseController
    {
        [JSInvokable("bgs")]
        async public static ValueTask<ResultModel> Index(string args)
        {
            string? sort = parse_arg("sort", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            string? html = await BongaCamsTo.InvokeHtml(AppInit.BongaCams.corsHost(), sort, pg, url => JsHttpClient.Get(url, addHeaders: new List<(string name, string val)>()
            {
                ("dnt", "1"),
                ("referer", AppInit.BongaCams.host!),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "same-origin"),
                ("x-requested-with", "XMLHttpRequest")
            }));

            if (html == null)
                return OnError("html");

            return OnResult(BongaCamsTo.Menu(null, sort), BongaCamsTo.Playlist(html, pl =>
            {
                pl.picture = rsizehost(pl.picture);
                return pl;
            }));
        }
    }
}

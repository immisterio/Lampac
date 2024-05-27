using JinEnergy.Engine;
using JinEnergy.Model;
using Microsoft.JSInterop;
using Shared.Engine.SISI;
using Shared.Model.Online;

namespace JinEnergy.SISI
{
    public class BongaCamsController : BaseController
    {
        [JSInvokable("bgs")]
        async public static ValueTask<ResultModel> Index(string args)
        {
            var init = AppInit.BongaCams.Clone();

            string? sort = parse_arg("sort", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            refresh: string? html = await BongaCamsTo.InvokeHtml(init.corsHost(), sort, pg, url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init, HeadersModel.Init(
                ("dnt", "1"),
                ("referer", init.host!),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "same-origin"),
                ("x-requested-with", "XMLHttpRequest")
            ))));

            var playlist = BongaCamsTo.Playlist(html, pl =>
            {
                pl.picture = rsizehost(pl.picture);
                pl.bookmark = null;
                return pl;
            });

            if (playlist.Count == 0 && IsRefresh(init, true))
                goto refresh;

            return OnResult(BongaCamsTo.Menu(null, sort), playlist);
        }
    }
}

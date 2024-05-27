using JinEnergy.Engine;
using JinEnergy.Model;
using Microsoft.JSInterop;
using Shared.Engine.SISI;
using Shared.Model.Online;

namespace JinEnergy.SISI
{
    public class HQpornerController : BaseController
    {
        [JSInvokable("hqr")]
        async public static ValueTask<ResultModel> Index(string args)
        {
            var init = AppInit.HQporner.Clone();

            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args);
            string? c = parse_arg("c", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            refresh: string? html = await HQpornerTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)));

            var playlist = HQpornerTo.Playlist("hqr/vidosik", html, pl =>
            {
                pl.picture = rsizehost(pl.picture);
                pl.bookmark = null;
                pl.related = false;
                return pl;
            });

            if (playlist.Count == 0 && IsRefresh(init, true))
                goto refresh;

            return OnResult(HQpornerTo.Menu(null, sort, c), playlist);
        }


        [JSInvokable("hqr/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var init = AppInit.HQporner.Clone();

            var head = HeadersModel.Init("referer", init.host!);

            refresh: var stream_links = await HQpornerTo.StreamLinks(init.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)), iframeurl => JsHttpClient.Get(init.cors(iframeurl), httpHeaders(args, init, head)));

            if (stream_links == null && IsRefresh(init, true))
                goto refresh;

            return OnResult(init, stream_links);
        }
    }
}

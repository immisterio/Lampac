using JinEnergy.Engine;
using JinEnergy.Model;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class XvideosController : BaseController
    {
        [JSInvokable("xds")]
        public static ValueTask<ResultModel> Index(string args) => result(args, "xds");

        [JSInvokable("xdsgay")]
        public static ValueTask<ResultModel> Gay(string args) => result(args, "xdsgay");

        [JSInvokable("xdssml")]
        public static ValueTask<ResultModel> Shemale(string args) => result(args, "xdssml");


        async static ValueTask<ResultModel> result(string args, string plugin)
        {
            var init = AppInit.Xvideos.Clone();

            string? search = parse_arg("search", args);
            string? c = parse_arg("c", args);
            string? sort = parse_arg("sort", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            refresh: string? html = await XvideosTo.InvokeHtml(init.corsHost(), plugin, search, sort, c, pg, url => JsHttpClient.Get(init.cors(url)));

            var playlist = XvideosTo.Playlist("xds/vidosik", html, pl =>
            {
                pl.picture = rsizehost(pl.picture);
                return pl;
            });

            if (playlist.Count == 0)
            {
                if (IsRefresh(init, true))
                    goto refresh;

                return OnError("playlist");
            }

            return OnResult(XvideosTo.Menu(null, plugin, sort, c), playlist);
        }


        [JSInvokable("xds/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var init = AppInit.Xvideos.Clone();

            refresh: var stream_links = await XvideosTo.StreamLinks("xds/vidosik", init.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(init.cors(url)), m3u => JsHttpClient.Get(init.cors(m3u)));

            if (stream_links == null)
            {
                if (IsRefresh(init, true))
                    goto refresh;

                return OnError("stream_links");
            }

            return OnResult(init, stream_links);
        }
    }
}

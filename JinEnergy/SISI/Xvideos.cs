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
            string? search = parse_arg("search", args);
            string? c = parse_arg("c", args);
            string? sort = parse_arg("sort", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            string? html = await XvideosTo.InvokeHtml(AppInit.Xvideos.corsHost(), plugin, search, sort, c, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return OnResult(XvideosTo.Menu(null, plugin, sort, c), XvideosTo.Playlist("xds/vidosik", html, pl =>
            {
                pl.picture = rsizehost(pl.picture);
                return pl;
            }));
        }


        [JSInvokable("xds/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var stream_links = await XvideosTo.StreamLinks(AppInit.Xvideos.corsHost(), parse_arg("uri", args), htmlurl => JsHttpClient.Get(htmlurl), m3url => JsHttpClient.Get(m3url));
            if (stream_links == null)
                return OnError("stream_links");

            return OnResult(stream_links);
        }
    }
}

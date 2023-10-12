using JinEnergy.Engine;
using JinEnergy.Model;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class XhamsterController : BaseController
    {
        [JSInvokable("xmr")]
        public static ValueTask<ResultModel> Index(string args) => result(args, "xmr");

        [JSInvokable("xmrgay")]
        public static ValueTask<ResultModel> Gay(string args) => result(args, "xmrgay");

        [JSInvokable("xmrsml")]
        public static ValueTask<ResultModel> Shemale(string args) => result(args, "xmrsml");


        async static ValueTask<ResultModel> result(string args, string plugin)
        {
            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args) ?? "newest";
            string? c = parse_arg("c", args);
            string? q = parse_arg("q", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1") + 1;

            string? html = await XhamsterTo.InvokeHtml(AppInit.Xhamster.corsHost(), plugin, search, c, q, sort, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return OnResult(XhamsterTo.Menu(null, plugin, c, q, sort), XhamsterTo.Playlist("xmr/vidosik", html, pl =>
            {
                pl.picture = rsizehost(pl.picture);
                return pl;
            }));
        }


        [JSInvokable("xmr/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var stream_links = await XhamsterTo.StreamLinks("xmr/vidosik", AppInit.Xhamster.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(url));
            if (stream_links == null)
                return OnError("stream_links");

            return OnResult(stream_links);
        }
    }
}

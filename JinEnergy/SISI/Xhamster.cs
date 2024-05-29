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
            var init = AppInit.Xhamster.Clone();

            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args) ?? "newest";
            string? c = parse_arg("c", args);
            string? q = parse_arg("q", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1") + 1;

            refresh: string? html = await XhamsterTo.InvokeHtml(init.corsHost(), plugin, search, c, q, sort, pg, url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)));

            var playlist = XhamsterTo.Playlist("xmr/vidosik", html, pl =>
            {
                pl.picture = rsizehost(pl.picture);
                return pl;
            });

            if (playlist.Count == 0 && IsRefresh(init))
                goto refresh;

            return OnResult(XhamsterTo.Menu(null, plugin, c, q, sort), playlist);
        }


        [JSInvokable("xmr/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var init = AppInit.Xhamster.Clone();

            refresh: var stream_links = await XhamsterTo.StreamLinks("xmr/vidosik", init.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)));

            if (stream_links == null && IsRefresh(init))
                goto refresh;

            if (bool.Parse(parse_arg("related", args) ?? "false"))
                return OnResult(null, stream_links?.recomends, total_pages: 1);

            return OnResult(init, stream_links);
        }
    }
}

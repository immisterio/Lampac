using JinEnergy.Engine;
using JinEnergy.Model;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class XnxxController : BaseController
    {
        [JSInvokable("xnx")]
        async public static ValueTask<ResultModel> Index(string args)
        {
            var init = AppInit.Xnxx.Clone();

            string? search = parse_arg("search", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            refresh: string? html = await XnxxTo.InvokeHtml(init.corsHost(), search, pg, url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)));

            var playlist = XnxxTo.Playlist("xnx/vidosik", html);

            if (playlist.Count == 0 && IsRefresh(init, true))
                goto refresh;

            return OnResult(XnxxTo.Menu(null), playlist);
        }


        [JSInvokable("xnx/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var init = AppInit.Xnxx.Clone();

            refresh: var stream_links = await XnxxTo.StreamLinks("xnx/vidosik", init.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)), url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)));

            if (stream_links == null && IsRefresh(init, true))
                goto refresh;

            if (bool.Parse(parse_arg("related", args) ?? "false"))
                return OnResult(null, stream_links?.recomends, total_pages: 1);

            return OnResult(init, stream_links);
        }
    }
}

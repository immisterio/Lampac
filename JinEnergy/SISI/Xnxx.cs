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
            var init = AppInit.Xnxx;

            string? search = parse_arg("search", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            string? html = await XnxxTo.InvokeHtml(init.corsHost(), search, pg, url => JsHttpClient.Get(init.cors(url)));
            if (html == null)
                return OnError("html");

            return OnResult(XnxxTo.Menu(null), XnxxTo.Playlist("xnx/vidosik", html, pl =>
            {
                pl.picture = rsizehost(pl.picture);
                return pl;
            }));
        }


        [JSInvokable("xnx/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var init = AppInit.Xnxx;

            var stream_links = await XnxxTo.StreamLinks("xnx/vidosik", init.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(init.cors(url)), url => JsHttpClient.Get(init.cors(url)));
            if (stream_links == null)
                return OnError("stream_links");

            return OnResult(init, stream_links);
        }
    }
}

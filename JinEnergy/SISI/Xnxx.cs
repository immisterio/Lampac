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
            string? search = parse_arg("search", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            string? html = await XnxxTo.InvokeHtml(AppInit.Xnxx.corsHost(), search, pg, url => JsHttpClient.Get(url));
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
            var stream_links = await XnxxTo.StreamLinks(AppInit.Xnxx.corsHost(), parse_arg("uri", args), htmlurl => JsHttpClient.Get(htmlurl), m3url => JsHttpClient.Get(m3url));
            if (stream_links == null)
                return OnError("stream_links");

            return OnResult(stream_links);
        }
    }
}

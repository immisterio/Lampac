using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class XnxxController : BaseController
    {
        [JSInvokable("xnx")]
        async public static ValueTask<dynamic> Index(string args)
        {
            string? search = parse_arg("search", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            string? html = await XnxxTo.InvokeHtml(AppInit.Xnxx.corsHost(), search, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return new
            {
                menu = XnxxTo.Menu(null),
                list = XnxxTo.Playlist("xnx/vidosik", html)
            };
        }


        [JSInvokable("xnx/vidosik")]
        async public static ValueTask<dynamic> Stream(string args)
        {
            var stream_links = await XnxxTo.StreamLinks(AppInit.Xnxx.corsHost(), parse_arg("uri", args), htmlurl => JsHttpClient.Get(htmlurl), m3url => JsHttpClient.Get(m3url));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

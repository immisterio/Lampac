using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class HQpornerController : BaseController
    {
        [JSInvokable("hqr")]
        async public static ValueTask<dynamic> Index(string args)
        {
            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args);
            string? c = parse_arg("c", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            string? html = await HQpornerTo.InvokeHtml(AppInit.HQporner.corsHost(), search, sort, c, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return OnResult(HQpornerTo.Playlist("hqr/vidosik", html), HQpornerTo.Menu(null, sort, c));
        }


        [JSInvokable("hqr/vidosik")]
        async public static ValueTask<dynamic> Stream(string args)
        {
            var stream_links = await HQpornerTo.StreamLinks(AppInit.HQporner.corsHost(), parse_arg("uri", args), htmlurl => JsHttpClient.Get(htmlurl), iframeurl => JsHttpClient.Get(AppInit.HQporner.corsHost(iframeurl)));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

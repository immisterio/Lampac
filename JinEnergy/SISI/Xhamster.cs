using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class XhamsterController : BaseController
    {
        [JSInvokable("xmr")]
        async public static ValueTask<dynamic> Index(string args)
        {
            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args) ?? "newest";
            int pg = int.Parse(parse_arg("pg", args) ?? "1") + 1;

            string? html = await XhamsterTo.InvokeHtml(AppInit.Xhamster.corsHost(), search, sort, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return OnResult(XhamsterTo.Playlist("xmr/vidosik", html), XhamsterTo.Menu(null, sort));
        }


        [JSInvokable("xmr/vidosik")]
        async public static ValueTask<dynamic> Stream(string args)
        {
            var stream_links = await XhamsterTo.StreamLinks("xmr/vidosik", AppInit.Xhamster.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(url));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

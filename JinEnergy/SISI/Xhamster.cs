using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class XhamsterController : BaseController
    {
        [JSInvokable("xmr")]
        async public static Task<dynamic> Index(string args)
        {
            string? search = arg("search", args);
            string? sort = arg("sort", args) ?? "newest";
            int pg = int.Parse(arg("pg", args) ?? "1") + 1;

            string? html = await XhamsterTo.InvokeHtml(AppInit.Xhamster.corsHost(), search, sort, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return new
            {
                menu = XhamsterTo.Menu(null, sort),
                list = XhamsterTo.Playlist("xmr/vidosik", html)
            };
        }


        [JSInvokable("xmr/vidosik")]
        async public static Task<dynamic> Stream(string args)
        {
            var stream_links = await XhamsterTo.StreamLinks(AppInit.Xhamster.corsHost(), arg("uri", args), url => JsHttpClient.Get(url));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

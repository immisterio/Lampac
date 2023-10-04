using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class PornHubController : BaseController
    {
        [JSInvokable("phub")]
        public static ValueTask<dynamic> Index(string args) => result(args, "phub");

        [JSInvokable("phubgay")]
        public static ValueTask<dynamic> Gay(string args) => result(args, "phubgay");

        [JSInvokable("phubsml")]
        public static ValueTask<dynamic> Shemale(string args) => result(args, "phubsml");


        async static ValueTask<dynamic> result(string args, string plugin)
        {
            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args);
            int c = int.Parse(parse_arg("c", args) ?? "0");
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            string? html = await PornHubTo.InvokeHtml(AppInit.PornHub.corsHost(), plugin, search, sort, c, null, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return OnResult(PornHubTo.Playlist("phub/vidosik", html), PornHubTo.Menu(null, plugin, sort, c));
        }


        [JSInvokable("phub/vidosik")]
        async public static ValueTask<dynamic> Stream(string args)
        {
            var stream_links = await PornHubTo.StreamLinks("phub/vidosik", AppInit.PornHub.corsHost(), parse_arg("vkey", args), url => JsHttpClient.Get(url));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

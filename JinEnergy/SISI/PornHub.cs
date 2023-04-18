using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class PornHubController : BaseController
    {
        [JSInvokable("phub")]
        async public static Task<dynamic> Index(string args)
        {
            string? search = arg("search", args);
            string? sort = arg("sort", args);
            int pg = int.Parse(arg("pg", args) ?? "1");

            string? html = await PornHubTo.InvokeHtml(AppInit.PornHub.corsHost(), search, sort, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return new
            {
                menu = PornHubTo.Menu(null, sort),
                list = PornHubTo.Playlist("phub/vidosik", html, picture => picture)
            };
        }


        [JSInvokable("phub/vidosik")]
        async public static Task<dynamic> Stream(string args)
        {
            var stream_links = await PornHubTo.StreamLinks(AppInit.PornHub.corsHost(), arg("vkey", args), url => JsHttpClient.Get(url));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

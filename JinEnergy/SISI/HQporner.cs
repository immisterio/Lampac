using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class HQpornerController : BaseController
    {
        [JSInvokable("hqr")]
        async public static Task<dynamic> Index(string args)
        {
            string? search = arg("search", args);
            string? sort = arg("sort", args);
            int pg = int.Parse(arg("pg", args) ?? "1");

            string? html = await HQpornerTo.InvokeHtml(AppInit.HQporner.corsHost(), search, sort, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return new
            {
                menu = HQpornerTo.Menu(null, sort),
                list = HQpornerTo.Playlist("hqr/vidosik", html)
            };
        }


        [JSInvokable("hqr/vidosik")]
        async public static Task<dynamic> Stream(string args)
        {
            var stream_links = await HQpornerTo.StreamLinks(AppInit.HQporner.corsHost(), arg("uri", args), htmlurl => JsHttpClient.Get(htmlurl), iframeurl => JsHttpClient.Get(AppInit.HQporner.corsHost(iframeurl)));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

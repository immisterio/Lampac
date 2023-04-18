using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class XvideosController : BaseController
    {
        [JSInvokable("xds")]
        async public static Task<dynamic> Index(string args)
        {
            string? search = arg("search", args);
            int pg = int.Parse(arg("pg", args) ?? "1");

            string? html = await XvideosTo.InvokeHtml(AppInit.Xvideos.host, search, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return new
            {
                menu = XvideosTo.Menu(null),
                list = XvideosTo.Playlist("xds/vidosik", html, picture => picture)
            };
        }


        [JSInvokable("xds/vidosik")]
        async public static Task<dynamic> Stream(string args)
        {
            var stream_links = await XvideosTo.StreamLinks(AppInit.Xvideos.host, arg("uri", args), htmlurl => JsHttpClient.Get(htmlurl), m3url => JsHttpClient.Get(m3url));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

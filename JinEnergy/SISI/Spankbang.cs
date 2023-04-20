using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class SpankbangController : BaseController
    {
        [JSInvokable("sbg")]
        async public static Task<dynamic> Index(string args)
        {
            string? search = arg("search", args);
            string? sort = arg("sort", args);
            int pg = int.Parse(arg("pg", args) ?? "1");

            string? html = await SpankbangTo.InvokeHtml(AppInit.Spankbang.corsHost(), search, sort, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return new
            {
                menu = SpankbangTo.Menu(null, sort),
                list = SpankbangTo.Playlist("sbg/vidosik", html)
            };
        }


        [JSInvokable("sbg/vidosik")]
        async public static Task<dynamic> Stream(string args)
        {
            var stream_links = await SpankbangTo.StreamLinks(AppInit.Spankbang.corsHost(), arg("uri", args), url => JsHttpClient.Get(url));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

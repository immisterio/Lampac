using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class SpankbangController : BaseController
    {
        [JSInvokable("sbg")]
        async public static ValueTask<dynamic> Index(string args)
        {
            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

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
        async public static ValueTask<dynamic> Stream(string args)
        {
            var stream_links = await SpankbangTo.StreamLinks("sbg/vidosik", AppInit.Spankbang.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(url));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

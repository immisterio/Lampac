using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class EbalovoController : BaseController
    {
        [JSInvokable("elo")]
        async public static ValueTask<dynamic> Index(string args)
        {
            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1") + 1;

            string? html = await EbalovoTo.InvokeHtml(AppInit.Ebalovo.corsHost(), search, sort, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return new
            {
                menu = EbalovoTo.Menu(null, sort),
                list = EbalovoTo.Playlist("elo/vidosik", html, pl => 
                {
                    pl.picture = $"https://vi.sisi.am/poster.jpg?href={pl.picture}&r=200";
                    return pl;
                })
            };
        }


        [JSInvokable("elo/vidosik")]
        async public static ValueTask<dynamic> Stream(string args)
        {
            var stream_links = await EbalovoTo.StreamLinks("elo/vidosik", AppInit.Ebalovo.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(url));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

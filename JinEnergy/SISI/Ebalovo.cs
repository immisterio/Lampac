using JinEnergy.Engine;
using JinEnergy.Model;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class EbalovoController : BaseController
    {
        [JSInvokable("elo")]
        async public static ValueTask<ResultModel> Index(string args)
        {
            var init = AppInit.Ebalovo;

            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args);
            string? c = parse_arg("c", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1") + 1;

            string? html = await EbalovoTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => JsHttpClient.Get(init.cors(url)));
            if (html == null)
                return OnError("html");

            return OnResult(EbalovoTo.Menu(null, sort, c), EbalovoTo.Playlist("elo/vidosik", html, pl =>
            {
                pl.picture = $"https://vi.sisi.am/poster.jpg?href={pl.picture}&r=200";
                return pl;
            }));
        }


        [JSInvokable("elo/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var init = AppInit.Ebalovo;

            var stream_links = await EbalovoTo.StreamLinks("elo/vidosik", init.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(init.cors(url)));
            if (stream_links == null)
                return OnError("stream_links");

            return OnResult(stream_links, isebalovo: true);
        }
    }
}

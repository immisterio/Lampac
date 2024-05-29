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
            var init = AppInit.Ebalovo.Clone();

            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args);
            string? c = parse_arg("c", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1") + 1;

            refresh: string? html = await EbalovoTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)));

            var playlist = EbalovoTo.Playlist("elo/vidosik", html, pl =>
            {
                pl.picture = $"https://vi.sisi.am/poster.jpg?href={pl.picture}&r=200";
                return pl;
            });

            if (playlist.Count == 0 && IsRefresh(init, true))
                goto refresh;

            return OnResult(EbalovoTo.Menu(null, sort, c), playlist);
        }


        [JSInvokable("elo/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var init = AppInit.Ebalovo.Clone();

            refresh: var stream_links = await EbalovoTo.StreamLinks("elo/vidosik", init.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)));

            if (stream_links == null && IsRefresh(init, true))
                goto refresh;

            if (bool.Parse(parse_arg("related", args) ?? "false"))
                return OnResult(null, stream_links?.recomends, total_pages: 1);

            return OnResult(init, stream_links, isebalovo: true);
        }
    }
}

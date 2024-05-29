using JinEnergy.Engine;
using JinEnergy.Model;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class SpankbangController : BaseController
    {
        [JSInvokable("sbg")]
        async public static ValueTask<ResultModel> Index(string args)
        {
            var init = AppInit.Spankbang.Clone();

            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            refresh: string? html = await SpankbangTo.InvokeHtml(init.corsHost(), search, sort, pg, url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)));

            var playlist = SpankbangTo.Playlist("sbg/vidosik", html);

            if (playlist.Count == 0 && IsRefresh(init))
                goto refresh;

            return OnResult(SpankbangTo.Menu(null, sort), playlist);
        }


        [JSInvokable("sbg/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var init = AppInit.Spankbang.Clone();

            refresh: var stream_links = await SpankbangTo.StreamLinks("sbg/vidosik", init.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)));

            if (stream_links == null && IsRefresh(init))
                goto refresh;

            if (bool.Parse(parse_arg("related", args) ?? "false"))
                return OnResult(null, stream_links?.recomends, total_pages: 1);

            return OnResult(init, stream_links);
        }
    }
}

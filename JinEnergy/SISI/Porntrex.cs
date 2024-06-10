using JinEnergy.Engine;
using JinEnergy.Model;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class PorntrexController : BaseController
    {
        [JSInvokable("ptx")]
        async public static ValueTask<ResultModel> Index(string args)
        {
            var init = AppInit.Porntrex.Clone();

            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args);
            string? c = parse_arg("c", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            refresh: string? html = await PorntrexTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)));

            var playlist = PorntrexTo.Playlist("ptx/vidosik", html);

            if (playlist.Count == 0 && IsRefresh(init))
                goto refresh;

            return OnResult(PorntrexTo.Menu(null, search, sort, c), playlist);
        }


        [JSInvokable("ptx/vidosik")]
        async public static ValueTask<dynamic> Stream(string args)
        {
            var init = AppInit.Porntrex.Clone();

            refresh: var stream_links = await PorntrexTo.StreamLinks(init.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)));

            if (stream_links == null && IsRefresh(init))
                goto refresh;

            return OnResult(init, stream_links);
        }
    }
}

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
            var init = AppInit.Porntrex;

            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args);
            string? c = parse_arg("c", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            string? html = await PorntrexTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => JsHttpClient.Get(init.cors(url)));
            if (html == null)
                return OnError("html");

            return OnResult(PorntrexTo.Menu(null, sort, c), PorntrexTo.Playlist("ptx/vidosik", html, pl =>
            {
                pl.picture = rsizehost(pl.picture);
                return pl;
            }));
        }


        [JSInvokable("ptx/vidosik")]
        async public static ValueTask<dynamic> Stream(string args)
        {
            var init = AppInit.Porntrex;

            var stream_links = await PorntrexTo.StreamLinks(init.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(init.cors(url)));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

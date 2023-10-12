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
            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args);
            string? c = parse_arg("c", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            string? html = await PorntrexTo.InvokeHtml(AppInit.Porntrex.corsHost(), search, sort, c, pg, url => JsHttpClient.Get(url));
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
            var stream_links = await PorntrexTo.StreamLinks(AppInit.Porntrex.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(url));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

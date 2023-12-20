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

            refresh: string? html = await PorntrexTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => JsHttpClient.Get(init.cors(url)));

            var playlist = PorntrexTo.Playlist("ptx/vidosik", html, pl =>
            {
                pl.picture = rsizehost(pl.picture);
                return pl;
            });

            if (playlist.Count == 0)
            {
                if (!init.corseu)
                {
                    init.corseu = true;
                    goto refresh;
                }

                return OnError("playlist");
            }

            return OnResult(PorntrexTo.Menu(null, sort, c), playlist);
        }


        [JSInvokable("ptx/vidosik")]
        async public static ValueTask<dynamic> Stream(string args)
        {
            var init = AppInit.Porntrex;

            refresh: var stream_links = await PorntrexTo.StreamLinks(init.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(init.cors(url)));

            if (stream_links == null)
            {
                if (!init.corseu)
                {
                    init.corseu = true;
                    goto refresh;
                }

                return OnError("stream_links");
            }

            return stream_links;
        }
    }
}

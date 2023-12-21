using JinEnergy.Engine;
using JinEnergy.Model;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class EpornerController : BaseController
    {
        [JSInvokable("epr")]
        async public static ValueTask<ResultModel> Index(string args)
        {
            var init = AppInit.Eporner.Clone();

            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args);
            string? c = parse_arg("c", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1") + 1;

            refresh: string? html = await EpornerTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => JsHttpClient.Get(init.cors(url)));

            var playlist = EpornerTo.Playlist("epr/vidosik", html, pl =>
            {
                pl.picture = pl.picture; // rsizehost(pl.picture);
                return pl;
            });

            if (playlist.Count == 0)
            {
                if (IsRefresh(init))
                    goto refresh;

                return OnError("playlist");
            }

            return OnResult(EpornerTo.Menu(null, sort, c), playlist);
        }


        [JSInvokable("epr/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var init = AppInit.Eporner.Clone();

            refresh: var stream_links = await EpornerTo.StreamLinks("epr/vidosik", init.corsHost(), parse_arg("uri", args), 
                            url => JsHttpClient.Get(init.cors(url)), 
                            jsonurl => JsHttpClient.Get(init.cors(jsonurl)));

            if (stream_links == null)
            {
                if (IsRefresh(init))
                    goto refresh;

                return OnError("stream_links");
            }

            return OnResult(init, stream_links);
        }
    }
}

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
            var init = AppInit.Eporner;

            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args);
            string? c = parse_arg("c", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1") + 1;

            string? html = await EpornerTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => JsHttpClient.Get(init.cors(url)));
            if (html == null)
                return OnError("html");

            return OnResult(EpornerTo.Menu(null, sort, c), EpornerTo.Playlist("epr/vidosik", html, pl =>
            {
                pl.picture = pl.picture; // rsizehost(pl.picture);
                return pl;
            }));
        }


        [JSInvokable("epr/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var init = AppInit.Eporner;

            var stream_links = await EpornerTo.StreamLinks("epr/vidosik", init.corsHost(), parse_arg("uri", args), 
                               url => JsHttpClient.Get(init.cors(url)), 
                               jsonurl => JsHttpClient.Get(init.cors(jsonurl)));

            if (stream_links == null)
                return OnError("stream_links");

            return OnResult(stream_links);
        }
    }
}

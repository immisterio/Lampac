using JinEnergy.Engine;
using JinEnergy.Model;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class HQpornerController : BaseController
    {
        [JSInvokable("hqr")]
        async public static ValueTask<ResultModel> Index(string args)
        {
            var init = AppInit.HQporner;

            string? search = parse_arg("search", args);
            string? sort = parse_arg("sort", args);
            string? c = parse_arg("c", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            string? html = await HQpornerTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => JsHttpClient.Get(init.cors(url)));
            if (html == null)
                return OnError("html");

            return OnResult(HQpornerTo.Menu(null, sort, c), HQpornerTo.Playlist("hqr/vidosik", html, pl =>
            {
                pl.picture = rsizehost(pl.picture);
                return pl;
            }));
        }


        [JSInvokable("hqr/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var init = AppInit.HQporner;

            var stream_links = await HQpornerTo.StreamLinks(init.corsHost(), parse_arg("uri", args), url => JsHttpClient.Get(init.cors(url)), iframeurl => JsHttpClient.Get(init.cors(iframeurl)));
            if (stream_links == null)
                return OnError("stream_links");

            return OnResult(init, stream_links);
        }
    }
}

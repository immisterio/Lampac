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

            refresh: string? html = await EpornerTo.InvokeHtml(init.corsHost(), search, sort, c, pg, url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)));

            var playlist = EpornerTo.Playlist("epr/vidosik", html);

            if (playlist.Count == 0 && IsRefresh(init))
                goto refresh;

            return OnResult(EpornerTo.Menu(null, search, sort, c), playlist);
        }


        [JSInvokable("epr/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var init = AppInit.Eporner.Clone();

            refresh: var stream_links = await EpornerTo.StreamLinks("epr/vidosik", init.corsHost(), parse_arg("uri", args), 
                            url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)), 
                            jsonurl => JsHttpClient.Get(init.cors(jsonurl), httpHeaders(args, init)));

            if (stream_links == null && IsRefresh(init))
                goto refresh;

            if (!init.streamproxy && AppInit.IsAndrod && JSRuntime != null)
            {
                try
                {
                    string player = await JSRuntime.InvokeAsync<string>("eval", "Lampa.Storage.field('player')");
                    init.streamproxy = player == "inner";
                }
                catch { }
            }

            if (bool.Parse(parse_arg("related", args) ?? "false"))
                return OnResult(null, stream_links?.recomends, total_pages: 1);

            return OnResult(init, stream_links);
        }
    }
}

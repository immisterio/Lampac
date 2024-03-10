using JinEnergy.Engine;
using JinEnergy.Model;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class ChaturbateController : BaseController
    {
        [JSInvokable("chu")]
        async public static ValueTask<ResultModel> Index(string args)
        {
            var init = AppInit.Chaturbate.Clone();

            string? sort = parse_arg("sort", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            refresh: string? html = await ChaturbateTo.InvokeHtml(init.corsHost(), sort, pg, url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)));

            var playlist = ChaturbateTo.Playlist("chu/potok", html);

            if (playlist.Count == 0 && IsRefresh(init))
                goto refresh;

            return OnResult(ChaturbateTo.Menu(null, sort), playlist);
        }


        [JSInvokable("chu/potok")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var init = AppInit.Chaturbate.Clone();

            refresh: var stream_links = await ChaturbateTo.StreamLinks(init.corsHost(), parse_arg("baba", args), url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init)));

            if (stream_links == null && IsRefresh(init))
                goto refresh;

            return OnResult(init, stream_links);
        }
    }
}

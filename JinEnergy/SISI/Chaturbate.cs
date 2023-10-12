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
            string? sort = parse_arg("sort", args);
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            string? html = await ChaturbateTo.InvokeHtml(AppInit.Chaturbate.corsHost(), sort, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return OnResult(ChaturbateTo.Menu(null, sort), ChaturbateTo.Playlist("chu/potok", html, pl =>
            {
                pl.picture = rsizehost(pl.picture);
                return pl;
            }));
        }


        [JSInvokable("chu/potok")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var stream_links = await ChaturbateTo.StreamLinks(AppInit.Chaturbate.corsHost(), parse_arg("baba", args), url => JsHttpClient.Get(url));
            if (stream_links == null)
                return OnError("stream_links");

            return OnResult(stream_links);
        }
    }
}

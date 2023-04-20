using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class ChaturbateController : BaseController
    {
        [JSInvokable("chu")]
        async public static Task<dynamic> Index(string args)
        {
            string? sort = arg("sort", args);
            int pg = int.Parse(arg("pg", args) ?? "1");

            string? html = await ChaturbateTo.InvokeHtml(AppInit.Chaturbate.corsHost(), sort, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return new
            {
                menu = ChaturbateTo.Menu(null, sort),
                list = ChaturbateTo.Playlist("chu/potok", html)
            };
        }


        [JSInvokable("chu/potok")]
        async public static Task<dynamic> Stream(string args)
        {
            var stream_links = await ChaturbateTo.StreamLinks(AppInit.Chaturbate.corsHost(), arg("baba", args), url => JsHttpClient.Get(url));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.SISI;

namespace JinEnergy.SISI
{
    public class EbalovoController : BaseController
    {
        [JSInvokable("elo")]
        async public static Task<dynamic> Index(string args)
        {
            string? search = arg("search", args);
            string? sort = arg("sort", args);
            int pg = int.Parse(arg("pg", args) ?? "1") + 1;

            string? html = await EbalovoTo.InvokeHtml(AppInit.Ebalovo.host, search, sort, pg, url => JsHttpClient.Get(url));
            if (html == null)
                return OnError("html");

            return new
            {
                menu = EbalovoTo.Menu(null, sort),
                list = EbalovoTo.Playlist("elo/vidosik", html, picture => picture)
            };
        }


        [JSInvokable("elo/vidosik")]
        async public static Task<dynamic> Stream(string args)
        {
            var stream_links = await EbalovoTo.StreamLinks(AppInit.Ebalovo.host, arg("uri", args), url => JsHttpClient.Get(url));
            if (stream_links == null)
                return OnError("stream_links");

            return stream_links;
        }
    }
}

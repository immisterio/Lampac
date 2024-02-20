using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using Shared.Model.Online;

namespace JinEnergy.Online
{
    public class CDNmoviesController : BaseController
    {
        [JSInvokable("lite/cdnmovies")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.CDNmovies.Clone();

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int t = int.Parse(parse_arg("t", args) ?? "0");
            int sid = int.Parse(parse_arg("sid", args) ?? "-1");

            if (arg.kinopoisk_id == 0)
                return EmptyError("kinopoisk_id");

            var oninvk = new CDNmoviesInvoke
            (
               null,
               init.corsHost(),
               ongettourl => JsHttpClient.Get(init.cors(ongettourl), httpHeaders(args, init, HeadersModel.Init(
                   ("DNT", "1"),
                   ("Upgrade-Insecure-Requests", "1")
               ))),
               streamfile => HostStreamProxy(init, streamfile)
            );

            string memkey = $"cdnmovies:view:{arg.kinopoisk_id}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.kinopoisk_id));

            string html = oninvk.Html(content, arg.kinopoisk_id, arg.title, arg.original_title, t, s, sid);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init))
                    goto refresh;
            }

            return html;
        }
    }
}

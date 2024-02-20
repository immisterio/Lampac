using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class CollapsController : BaseController
    {
        [JSInvokable("lite/collaps")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Collaps.Clone();

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");

            if (arg.kinopoisk_id == 0 && string.IsNullOrWhiteSpace(arg.imdb_id))
                return EmptyError("imdb_id");

            var oninvk = new CollapsInvoke
            (
               null,
               init.corsHost(),
               init.dash,
               ongettourl => JsHttpClient.Get(init.cors(ongettourl), httpHeaders(args, init)),
               streamfile => HostStreamProxy(init, streamfile)
            );

            string memkey = $"collaps:view:{arg.imdb_id}:{arg.kinopoisk_id}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.imdb_id, arg.kinopoisk_id));

            string html = oninvk.Html(content, arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, s);
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

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
            var init = AppInit.Collaps;

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");

            if (arg.kinopoisk_id == 0 && string.IsNullOrWhiteSpace(arg.imdb_id))
                return EmptyError("imdb_id");

            var oninvk = new CollapsInvoke
            (
               null,
               init.corsHost(),
               ongettourl => JsHttpClient.Get(init.cors(ongettourl)),
               onstreamtofile => onstreamtofile
            );

            var content = await InvokeCache(arg.id, $"collaps:view:{arg.imdb_id}:{arg.kinopoisk_id}", () => oninvk.Embed(arg.imdb_id, arg.kinopoisk_id));
            if (content == null)
                return EmptyError("content");

            return oninvk.Html(content, arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, s);
        }
    }
}

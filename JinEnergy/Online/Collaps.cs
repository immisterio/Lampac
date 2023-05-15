using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class CollapsController : BaseController
    {
        [JSInvokable("lite/collaps")]
        async public static Task<string> Index(string args)
        {
            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");

            if (arg.kinopoisk_id == 0 && string.IsNullOrWhiteSpace(arg.imdb_id))
                return OnError("imdb_id");

            var oninvk = new CollapsInvoke
            (
               null,
               AppInit.Collaps.corsHost(),
               ongettourl => JsHttpClient.Get(AppInit.Collaps.corsHost(ongettourl)),
               onstreamtofile => onstreamtofile
            );

            var content = await InvokeCache(arg.id, $"collaps:view:{arg.imdb_id}:{arg.kinopoisk_id}", () => oninvk.Embed(arg.imdb_id, arg.kinopoisk_id));
            if (content == null)
                return OnError("content");

            return oninvk.Html(content, arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, s);
        }
    }
}

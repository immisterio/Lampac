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
            int s = int.Parse(arg("s", args) ?? "0");
            defaultOnlineArgs(args, out long id, out string? imdb_id, out long kinopoisk_id, out string? title, out string? original_title, out int serial, out string? original_language, out int year, out string? source, out int clarification, out long cub_id, out string? account_email);

            var oninvk = new CollapsInvoke
            (
               null,
               AppInit.Collaps.corsHost(),
               ongettourl => JsHttpClient.Get(AppInit.Collaps.corsHost(ongettourl)),
               onstreamtofile => onstreamtofile
            );

            var content = await InvokeCache(id, $"collaps:view:{imdb_id}:{kinopoisk_id}", () => oninvk.Embed(imdb_id, kinopoisk_id));
            if (content == null)
                return OnError("content");

            return oninvk.Html(content, imdb_id, kinopoisk_id, title, original_title, s);
        }
    }
}

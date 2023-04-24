using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class AshdiController : BaseController
    {
        [JSInvokable("lite/ashdi")]
        async public static Task<string> Index(string args)
        {
            int s = int.Parse(arg("s", args) ?? "-1");
            int t = int.Parse(arg("t", args) ?? "-1");
            defaultOnlineArgs(args, out long id, out string? imdb_id, out long kinopoisk_id, out string? title, out string? original_title, out int serial, out string? original_language, out int year, out string? source, out int clarification, out long cub_id, out string? account_email);

            var oninvk = new AshdiInvoke
            (
               null,
               AppInit.Ashdi.corsHost(),
               ongettourl => JsHttpClient.Get(AppInit.Ashdi.corsHost(ongettourl)),
               onstreamtofile => onstreamtofile
            );

            var content = await InvokeCache(id, $"ashdi:view:{kinopoisk_id}", () => oninvk.Embed(kinopoisk_id));
            if (content == null)
                return OnError("content");

            return oninvk.Html(content, kinopoisk_id, title, original_title, t, s);
        }
    }
}

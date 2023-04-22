using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class EneyidaController : BaseController
    {
        [JSInvokable("lite/eneyida")]
        async public static Task<dynamic> Index(string args)
        {
            string? href = arg("href", args);
            int s = int.Parse(arg("s", args) ?? "-1");
            int t = int.Parse(arg("t", args) ?? "-1");
            defaultOnlineArgs(args, out long id, out string? imdb_id, out long kinopoisk_id, out string? title, out string? original_title, out int serial, out string? original_language, out int year, out string? source, out int clarification, out long cub_id, out string? account_email);

            if (original_language != "en")
                clarification = 1;

            var oninvk = new EneyidaInvoke
            (
               null,
               AppInit.Eneyida.corsHost(),
               ongettourl => JsHttpClient.Get(AppInit.Eneyida.corsHost(ongettourl)),
               (url, data) => JsHttpClient.Post(AppInit.Eneyida.corsHost(url), data),
               onstreamtofile => onstreamtofile
               //AppInit.log
            );

            var result = await InvokeCache(id, $"eneyida:view:{original_title}:{year}:{href}", () => oninvk.Embed(clarification == 1 ? title : original_title, year, href));
            if (result == null)
                return OnError("result");

            return oninvk.Html(result, title, original_title, year, t, s, href);
        }
    }
}

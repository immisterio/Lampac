using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class KinobaseController : BaseController
    {
        [JSInvokable("lite/kinobase")]
        async public static Task<string> Index(string args)
        {
            int s = int.Parse(arg("s", args) ?? "-1");
            defaultOnlineArgs(args, out long id, out string? imdb_id, out long kinopoisk_id, out string? title, out string? original_title, out int serial, out string? original_language, out int year, out string? source, out int clarification, out long cub_id, out string? account_email);

            var oninvk = new KinobaseInvoke
            (
               null,
               AppInit.Kinobase.corsHost(),
               ongettourl => JsHttpClient.Get(AppInit.Kinobase.corsHost(ongettourl)),
               (url, data) => JsHttpClient.Post(AppInit.Kinobase.corsHost(url), data),
               streamfile => streamfile
            );

            var content = await InvokeCache(id, $"kinobase:view:{title}:{year}", () => oninvk.Embed(title, year, evalcode => JSRuntime.InvokeAsync<string?>("eval", evalcode.Replace("}eval(", "}("))));
            if (content == null)
                return string.Empty;

            return oninvk.Html(content, title, year, s);
        }
    }
}

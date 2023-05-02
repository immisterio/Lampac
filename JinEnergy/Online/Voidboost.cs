using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class VoidboostController : BaseController
    {
        #region VoidboostInvoke
        static VoidboostInvoke oninvk = new VoidboostInvoke
        (
            null,
            AppInit.Voidboost.corsHost(),
            ongettourl => JsHttpClient.Get(AppInit.Voidboost.corsHost(ongettourl)),
            (url, data) => JsHttpClient.Post(AppInit.Voidboost.corsHost(url), data),
            streamfile => streamfile
        );
        #endregion

        [JSInvokable("lite/voidboost")]
        async public static Task<string> Index(string args)
        {
            string? t = arg("t", args);
            defaultOnlineArgs(args, out long id, out string? imdb_id, out long kinopoisk_id, out string? title, out string? original_title, out int serial, out string? original_language, out int year, out string? source, out int clarification, out long cub_id, out string? account_email);

            var content = await InvokeCache(id, $"voidboost:view:{kinopoisk_id}:{imdb_id}:{t}", () => oninvk.Embed(imdb_id, kinopoisk_id, t));
            if (content == null)
                return string.Empty;

            return oninvk.Html(content, imdb_id, kinopoisk_id, title, original_title, t);
        }


        #region Serial
        [JSInvokable("lite/voidboost/serial")]
        async public static Task<string> Serial(string args)
        {
            string? t = arg("t", args);
            int s = int.Parse(arg("s", args) ?? "0");
            defaultOnlineArgs(args, out long id, out string? imdb_id, out long kinopoisk_id, out string? title, out string? original_title, out int serial, out string? original_language, out int year, out string? source, out int clarification, out long cub_id, out string? account_email);

            string? html = await InvokeCache(id, $"voidboost:view:serial:{t}:{s}", () => oninvk.Serial(imdb_id, kinopoisk_id, title, original_title, t, s, false));
            if (html == null)
                return string.Empty;

            return html;
        }
        #endregion

        #region Movie
        [JSInvokable("lite/voidboost/movie")]
        async public static Task<string> Movie(string args)
        {
            string? t = arg("t", args);
            int s = int.Parse(arg("s", args) ?? "0");
            int e = int.Parse(arg("e", args) ?? "0");

            string? result = await InvokeCache(0, $"rezka:view:stream:{t}:{s}:{e}", () => oninvk.Movie(arg("title", args), arg("original_title", args), t, s, e, false));
            if (result == null)
                return string.Empty;

            return result;
        }
        #endregion
    }
}

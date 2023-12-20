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
            ongettourl => JsHttpClient.Get(AppInit.Voidboost.cors(ongettourl)),
            (url, data) => JsHttpClient.Post(AppInit.Voidboost.cors(url), data),
            streamfile => HostStreamProxy(AppInit.Voidboost, streamfile)
        );
        #endregion

        [JSInvokable("lite/voidboost")]
        async public static ValueTask<string> Index(string args)
        {
            var arg = defaultArgs(args);
            string? t = parse_arg("t", args);

            if (arg.kinopoisk_id == 0 && string.IsNullOrWhiteSpace(arg.imdb_id))
                return EmptyError("arg");

            var content = await InvokeCache(arg.id, $"voidboost:view:{arg.kinopoisk_id}:{arg.imdb_id}:{t}", () => oninvk.Embed(arg.imdb_id, arg.kinopoisk_id, t));
            if (content == null)
                return EmptyError("content");

            return oninvk.Html(content, arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, t);
        }


        #region Serial
        [JSInvokable("lite/voidboost/serial")]
        async public static ValueTask<string> Serial(string args)
        {
            var arg = defaultArgs(args);
            string? t = parse_arg("t", args);
            int s = int.Parse(parse_arg("s", args) ?? "0");

            if (string.IsNullOrWhiteSpace(t))
                return EmptyError("t");

            string? html = await InvokeCache(0, $"voidboost:view:serial:{t}:{s}", () => oninvk.Serial(arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, t, s, false));
            if (html == null)
                return string.Empty;

            return html;
        }
        #endregion

        #region Movie
        [JSInvokable("lite/voidboost/movie")]
        async public static ValueTask<string> Movie(string args)
        {
            string? t = parse_arg("t", args);
            int s = int.Parse(parse_arg("s", args) ?? "0");
            int e = int.Parse(parse_arg("e", args) ?? "0");

            var md = await InvokeCache(0, $"rezka:view:stream:{t}:{s}:{e}", () => oninvk.Movie(t, s, e));
            if (md == null)
                return EmptyError("md");

            string? result = oninvk.Movie(md, parse_arg("title", args), parse_arg("original_title", args), false);
            if (result == null)
                return EmptyError("result");

            return result;
        }
        #endregion
    }
}

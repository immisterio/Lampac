using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using Shared.Model.Online.Rezka;

namespace JinEnergy.Online
{
    public class RezkaController : BaseController
    {
        #region RezkaInvoke
        static RezkaInvoke oninvk = new RezkaInvoke
        (
            null,
            AppInit.Rezka.corsHost(),
            ongettourl => JsHttpClient.Get(AppInit.Rezka.corsHost(ongettourl)),
            (url, data) => JsHttpClient.Post(AppInit.Rezka.corsHost(url), data),
            streamfile => streamfile
        );
        #endregion

        [JSInvokable("lite/rezka")]
        async public static ValueTask<string> Index(string args)
        {
            var arg = defaultArgs(args);
            string? t = parse_arg("t", args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            string? href = parse_arg("href", args);

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(arg.title) || arg.year == 0))
                return EmptyError("arg");

            int clarification = arg.clarification;
            if (arg.original_language != "en")
                clarification = 1;

            var content = await InvokeCache(arg.id, $"rezka:view:{arg.title}:{arg.original_title}:{arg.year}:{clarification}:{href}", () => oninvk.Embed(arg.title, arg.original_title, clarification, arg.year, href));
            if (content == null)
                return EmptyError("content");

            return oninvk.Html(content, arg.title, arg.original_title, clarification, arg.year, s, href, false);
        }


        #region Serial
        [JSInvokable("lite/rezka/serial")]
        async public static ValueTask<string> Serial(string args)
        {
            var arg = defaultArgs(args);
            int t = int.Parse(parse_arg("t", args) ?? "0");
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            string? href = parse_arg("href", args);

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(arg.title) || arg.year == 0))
                return EmptyError("arg");

            Episodes? root = await InvokeCache(0, $"rezka:view:serial:{arg.id}:{t}", () => oninvk.SerialEmbed(arg.id, t));
            if (root == null)
                return EmptyError("root");

            int clarification = arg.clarification;
            if (arg.original_language != "en")
                clarification = 1;

            var content = await InvokeCache(0, $"rezka:view:{arg.title}:{arg.original_title}:{arg.year}:{clarification}:{href}", () => oninvk.Embed(arg.title, arg.original_title, clarification, arg.year, href));
            if (content == null)
                return EmptyError("content");

            return oninvk.Serial(root, content, arg.title, arg.original_title, clarification, arg.year, href, arg.id, t, s, false);
        }
        #endregion

        #region Movie
        [JSInvokable("lite/rezka/movie")]
        async public static ValueTask<string> Movie(string args)
        {
            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int e = int.Parse(parse_arg("e", args) ?? "-1");
            int t = int.Parse(parse_arg("t", args) ?? "0");
            int director = int.Parse(parse_arg("director", args) ?? "0");

            string? result = await InvokeCache(0, $"rezka:view:get_cdn_series:{arg.id}:{t}:{director}:{s}:{e}", () => oninvk.Movie(arg.title, arg.original_title, arg.id, t, director, s, e, parse_arg("favs", args), false));
            if (result == null)
                return EmptyError("result");

            return result;
        }
        #endregion
    }
}

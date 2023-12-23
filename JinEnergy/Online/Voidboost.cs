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
            AppInit.Voidboost.hls,
            ongettourl => JsHttpClient.Get(AppInit.Voidboost.cors(ongettourl)),
            (url, data) => JsHttpClient.Post(AppInit.Voidboost.cors(url), data),
            streamfile => HostStreamProxy(AppInit.Voidboost, streamfile)
        );
        #endregion

        [JSInvokable("lite/voidboost")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Voidboost.Clone();

            var arg = defaultArgs(args);
            string? t = parse_arg("t", args);

            if (arg.kinopoisk_id == 0 && string.IsNullOrWhiteSpace(arg.imdb_id))
                return EmptyError("arg");

            string memkey = $"voidboost:{arg.kinopoisk_id}:{arg.imdb_id}:{t}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.imdb_id, arg.kinopoisk_id, t));

            string html = oninvk.Html(content, arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, t);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init, true))
                    goto refresh;
            }

            return html;
        }


        #region Serial
        [JSInvokable("lite/voidboost/serial")]
        async public static ValueTask<string> Serial(string args)
        {
            var init = AppInit.Voidboost.Clone();

            var arg = defaultArgs(args);
            string? t = parse_arg("t", args);
            int s = int.Parse(parse_arg("s", args) ?? "0");

            if (string.IsNullOrWhiteSpace(t))
                return EmptyError("t");

            string memkey = $"voidboost:serial:{t}:{s}";
            refresh: string? html = await InvokeCache(0, memkey, () => oninvk.Serial(arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, t, s, false));
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init, true))
                    goto refresh;
            }

            return html ?? string.Empty;
        }
        #endregion

        #region Movie
        [JSInvokable("lite/voidboost/movie")]
        async public static ValueTask<string> Movie(string args)
        {
            var init = AppInit.Voidboost.Clone();

            string? t = parse_arg("t", args);
            int s = int.Parse(parse_arg("s", args) ?? "0");
            int e = int.Parse(parse_arg("e", args) ?? "0");

            string memkey = $"rezka:stream:{t}:{s}:{e}";
            refresh: var md = await InvokeCache(0, memkey, () => oninvk.Movie(t, s, e));

            string? result = oninvk.Movie(md, parse_arg("title", args), parse_arg("original_title", args), false);
            if (string.IsNullOrEmpty(result))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init, true))
                    goto refresh;
            }

            return result ?? string.Empty;
        }
        #endregion
    }
}

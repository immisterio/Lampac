using JinEnergy.Engine;
using Lampac.Models.LITE;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using Shared.Model.Base;

namespace JinEnergy.Online
{
    public class VoidboostController : BaseController
    {
        #region VoidboostInvoke
        static VoidboostInvoke voidboostInvoke(string args, RezkaSettings init)
        {
            return new VoidboostInvoke
            (
                null,
                init.corsHost(),
                MaybeInHls(init.hls, init),
                ongettourl => JsHttpClient.Get(init.cors(ongettourl), httpHeaders(args, init)),
                (url, data) => JsHttpClient.Post(init.cors(url), data, httpHeaders(args, init)),
                streamfile => HostStreamProxy(init, streamfile)
            );
        }
        #endregion

        [JSInvokable("lite/voidboost")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Voidboost.Clone();

            var arg = defaultArgs(args);
            string? t = parse_arg("t", args);

            if (arg.kinopoisk_id == 0 && string.IsNullOrWhiteSpace(arg.imdb_id))
                return EmptyError("arg");

            var oninvk = voidboostInvoke(args, init);

            string memkey = $"voidboost:{arg.kinopoisk_id}:{arg.imdb_id}:{t}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.imdb_id, arg.kinopoisk_id, t));

            string html = oninvk.Html(content, arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, t);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init))
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

            var oninvk = voidboostInvoke(args, init);

            string memkey = $"voidboost:serial:{t}:{s}";
            refresh: string? html = await InvokeCache(0, memkey, () => oninvk.Serial(arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, t, s, false));
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init))
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

            var oninvk = voidboostInvoke(args, init);

            string memkey = $"rezka:stream:{t}:{s}:{e}";
            refresh: var md = await InvokeCache(0, memkey, () => oninvk.Movie(t, s, e));

            string? result = oninvk.Movie(md, parse_arg("title", args), parse_arg("original_title", args), false);
            if (string.IsNullOrEmpty(result))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init))
                    goto refresh;
            }

            return result ?? string.Empty;
        }
        #endregion
    }
}

using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class ZetflixController : BaseController
    {
        [JSInvokable("lite/zetflix")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Zetflix.Clone();
            bool userapn = IsApnIncluded(init);

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            string? t = parse_arg("t", args);

            var oninvk = new ZetflixInvoke
            (
               null,
               init.corsHost(),
               MaybeInHls(init.hls, init),
               (url, head) => JsHttpClient.Get(init.cors(url), httpHeaders(args, init, head)),
               streamfile => userapn ? HostStreamProxy(init, streamfile) : DefaultStreamProxy(streamfile)
               //AppInit.log
            );

            string memkey = $"zetfix:view:{arg.kinopoisk_id}:{s}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.kinopoisk_id, s));

            int number_of_seasons = 1;
            if (content?.pl != null && !content.movie && s == -1 && arg.id > 0)
                number_of_seasons = await InvStructCache(arg.id, $"zetfix:number_of_seasons:{arg.kinopoisk_id}", () => oninvk.number_of_seasons(arg.id));

            string html = oninvk.Html(content, number_of_seasons, arg.kinopoisk_id, arg.title, arg.original_title, t, s);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init))
                    goto refresh;
            }

            return html;
        }
    }
}

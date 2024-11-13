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

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            string? t = parse_arg("t", args);
            int serial = int.Parse(parse_arg("serial", args) ?? "0");

            var oninvk = new ZetflixInvoke
            (
               null,
               init.corsHost(),
               MaybeInHls(init.hls, init),
               (url, head) => JsHttpClient.Get(init.cors(url), httpHeaders(args, init, head)),
               streamfile => HostStreamProxy(init, streamfile)
               //AppInit.log
            );

            int rs = serial == 1 ? (s == -1 ? 1 : s) : s;
            string memkey = $"zetfix:view:{arg.kinopoisk_id}:{rs}";

            var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.kinopoisk_id, rs));

            int number_of_seasons = 1;
            if (content?.pl != null && !content.movie && s == -1 && arg.id > 0)
                number_of_seasons = await InvStructCache(arg.id, $"zetfix:number_of_seasons:{arg.kinopoisk_id}", () => oninvk.number_of_seasons(arg.id));

            return oninvk.Html(content, number_of_seasons, arg.kinopoisk_id, arg.title, arg.original_title, t, s, isbwa: true);
        }
    }
}

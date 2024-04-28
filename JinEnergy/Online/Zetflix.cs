using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class ZetflixController : BaseController
    {
        static bool origstream;

        static string? last_check_url;

        [JSInvokable("lite/zetflix")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Zetflix.Clone();
            bool userapn = IsApnIncluded(init);

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
               streamfile => userapn ? HostStreamProxy(init, streamfile) : DefaultStreamProxy(streamfile, origstream)
               //AppInit.log
            );

            int rs = serial == 1 ? (s == -1 ? 1 : s) : s;
            string memkey = $"zetfix:view:{arg.kinopoisk_id}:{rs}";

            refresh: var content = await InvokeCache(arg.id, memkey, async () => 
            {
                string? html = await JsHttpClient.Get("https://bwa-cloud.apn.monster/lite/zetflix"+$"?kinopoisk_id={arg.kinopoisk_id}&serial={serial}&s={s}&origsource=true");
                return oninvk.Embed(html);
            });

            int number_of_seasons = 1;
            if (content?.pl != null && !content.movie && s == -1 && arg.id > 0)
                number_of_seasons = await InvStructCache(arg.id, $"zetfix:number_of_seasons:{arg.kinopoisk_id}", () => oninvk.number_of_seasons(arg.id));

            if (content?.check_url != null && !userapn)
            {
                if (last_check_url != content.check_url)
                {
                    last_check_url = content.check_url;
                    origstream = await IsOrigStream(content.check_url, 4);
                }
            }

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

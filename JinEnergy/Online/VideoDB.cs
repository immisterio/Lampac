using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class VideoDBController : BaseController
    {
        [JSInvokable("lite/videodb")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.VideoDB.Clone();
            bool userapn = IsApnIncluded(init);

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int sid = int.Parse(parse_arg("sid", args) ?? "-1");
            string? t = parse_arg("t", args);

            var oninvk = new VideoDBInvoke
            (
               null,
               init.corsHost(),
               MaybeInHls(init.hls, init),
               (url, head) => JsHttpClient.Get(init.cors(url), httpHeaders(args, init, head)),
               streamfile => userapn ? HostStreamProxy(init, streamfile) : DefaultStreamProxy(streamfile)
               //AppInit.log
            );

            string memkey = $"videodb:view:{arg.kinopoisk_id}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.kinopoisk_id));

            string html = oninvk.Html(content, arg.kinopoisk_id, arg.title, arg.original_title, t, s, sid);
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

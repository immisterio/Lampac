using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class VideoDBController : BaseController
    {
        #region VideoDBController
        static bool origstream;

        static long lastcheckid;
        #endregion

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
               init.hls,
               (url, head) => JsHttpClient.Get(init.cors(url), addHeaders: head),
               streamfile => userapn ? HostStreamProxy(init, streamfile) : DefaultStreamProxy(streamfile, origstream)
               //AppInit.log
            );

            string memkey = $"videodb:view:{arg.kinopoisk_id}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.kinopoisk_id, arg.serial));

            if (!userapn && AppInit.Country == "UA" && lastcheckid != arg.kinopoisk_id)
            {
                string? uri = oninvk.FirstLink(content, t, s, sid);
                if (!string.IsNullOrEmpty(uri))
                {
                    lastcheckid = arg.kinopoisk_id;
                    origstream = await IsOrigStream(uri);
                }
            }

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

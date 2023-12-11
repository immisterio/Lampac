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
            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int sid = int.Parse(parse_arg("sid", args) ?? "-1");
            string? t = parse_arg("t", args);

            if (arg.kinopoisk_id == 0)
                return EmptyError("kinopoisk_id");

            var oninvk = new VideoDBInvoke
            (
               null,
               AppInit.VideoDB.corsHost(),
               (url, head) => JsHttpClient.Get(AppInit.VideoDB.corsHost(url), addHeaders: head),
               streamfile => HostStreamProxy(streamfile, origstream)
               //AppInit.log
            );

            var content = await InvokeCache(arg.id, $"videodb:view:{arg.kinopoisk_id}", () => oninvk.Embed(arg.kinopoisk_id, arg.serial));
            if (content?.pl == null)
                return EmptyError("content");

            if (AppInit.Country == "UA" && lastcheckid != arg.kinopoisk_id)
            {
                string? uri = oninvk.FirstLink(content, t, s, sid);
                if (!string.IsNullOrEmpty(uri))
                {
                    lastcheckid = arg.kinopoisk_id;
                    origstream = await IsOrigStream(uri);
                }
            }

            return oninvk.Html(content, arg.kinopoisk_id, arg.title, arg.original_title, t, s, sid);
        }
    }
}

using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class VideoDBController : BaseController
    {
        [JSInvokable("lite/videodb")]
        async public static Task<string> Index(string args)
        {
            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int sid = int.Parse(parse_arg("sid", args) ?? "-1");
            string? t = parse_arg("t", args);

            if (arg.kinopoisk_id == 0)
                return OnError("kinopoisk_id");

            var oninvk = new VideoDBInvoke
            (
               null,
               (url, head) => JsHttpClient.Get(url, addHeaders: head),
               onstreamtofile => onstreamtofile
               //AppInit.log
            );

            var content = await InvokeCache(arg.id, $"videodb:view:{arg.kinopoisk_id}", () => oninvk.Embed(arg.kinopoisk_id, arg.serial));
            if (content?.pl == null)
                return OnError("content");

            return oninvk.Html(content, arg.kinopoisk_id, arg.title, arg.original_title, t, s, sid);
        }
    }
}

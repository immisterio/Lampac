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
            int s = int.Parse(arg("s", args) ?? "-1");
            int sid = int.Parse(arg("sid", args) ?? "-1");
            string? t = arg("t", args);
            defaultOnlineArgs(args, out long id, out string? imdb_id, out long kinopoisk_id, out string? title, out string? original_title, out int serial, out string? original_language, out int year, out string? source, out int clarification, out long cub_id, out string? account_email);

            var oninvk = new VideoDBInvoke
            (
               null,
               (url, head) => JsHttpClient.Get(url, addHeaders: head),
               onstreamtofile => onstreamtofile
               //AppInit.log
            );

            var content = await InvokeCache(id, $"videodb:view:{kinopoisk_id}", () => oninvk.Embed(kinopoisk_id, serial));
            if (content?.pl == null)
                return OnError("content");

            return oninvk.Html(content, kinopoisk_id, title, original_title, t, s, sid);
        }
    }
}

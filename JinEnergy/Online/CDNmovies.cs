using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class CDNmoviesController : BaseController
    {
        [JSInvokable("lite/cdnmovies")]
        async public static Task<string> Index(string args)
        {
            int s = int.Parse(arg("s", args) ?? "-1");
            int t = int.Parse(arg("t", args) ?? "0");
            int sid = int.Parse(arg("sid", args) ?? "-1");
            defaultOnlineArgs(args, out long id, out string? imdb_id, out long kinopoisk_id, out string? title, out string? original_title, out int serial, out string? original_language, out int year, out string? source, out int clarification, out long cub_id, out string? account_email);

            var oninvk = new CDNmoviesInvoke
            (
               null,
               AppInit.CDNmovies.corsHost(),
               ongettourl => JsHttpClient.Get(AppInit.CDNmovies.corsHost(ongettourl), addHeaders: new List<(string name, string val)>()
               {
                   ("DNT", "1"),
                   ("Upgrade-Insecure-Requests", "1")
               }),
               onstreamtofile => onstreamtofile
            );

            var voices = await InvokeCache(id, $"cdnmovies:view:{kinopoisk_id}", () => oninvk.Embed(kinopoisk_id));
            if (voices == null)
                return OnError("content");

            return oninvk.Html(voices, kinopoisk_id, title, original_title, t, s, sid);
        }
    }
}

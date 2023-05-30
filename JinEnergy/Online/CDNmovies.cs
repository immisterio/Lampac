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
            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int t = int.Parse(parse_arg("t", args) ?? "0");
            int sid = int.Parse(parse_arg("sid", args) ?? "-1");

            if (arg.kinopoisk_id == 0)
                return OnError("kinopoisk_id");

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

            var content = await InvokeCache(arg.id, $"cdnmovies:view:{arg.kinopoisk_id}", () => oninvk.Embed(arg.kinopoisk_id));
            if (content == null)
                return OnError("content");

            return oninvk.Html(content, arg.kinopoisk_id, arg.title, arg.original_title, t, s, sid);
        }
    }
}

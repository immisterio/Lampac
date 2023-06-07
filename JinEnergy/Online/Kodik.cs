using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class KodikController : BaseController
    {
        #region KodikInvoke
        static KodikInvoke oninvk = new KodikInvoke
        (
            null,
            AppInit.Kodik.apihost!,
            AppInit.Kodik.token,
            (uri, head) => JsHttpClient.Get(AppInit.Kodik.corsHost(uri), addHeaders: head),
            (uri, data) => JsHttpClient.Post(AppInit.Kodik.corsHost(uri), data),
            onstreamtofile => onstreamtofile
            //AppInit.log
        );
        #endregion

        [JSInvokable("lite/kodik")]
        async public static Task<string> Index(string args)
        {
            var arg = defaultArgs(args);
            string? kid = parse_arg("kid", args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");

            if (arg.kinopoisk_id == 0 && string.IsNullOrWhiteSpace(arg.imdb_id))
                return OnError("arg");

            var content = await InvokeCache(arg.id, $"kodik:view:{arg.kinopoisk_id}:{arg.imdb_id}", () => oninvk.Embed(arg.imdb_id, arg.kinopoisk_id, s));
            if (content == null || content.Count == 0)
                return OnError("content");

            return oninvk.Html(content, arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, kid, s, false);
        }


        #region VideoParse
        [JSInvokable("lite/kodik/video")]
        async public static Task<string> VideoParse(string args)
        {
            var arg = defaultArgs(args);
            int episode = int.Parse(parse_arg("episode", args) ?? "0");
            string? link = parse_arg("link", args);
            if (link == null)
                return OnError("link");

            string? result = await InvokeCache(0, $"kodik:video:{link}", () => oninvk.VideoParse(AppInit.Kodik.linkhost, arg.title, arg.original_title, link, episode, false));
            if (result == null)
                return OnError("result");

            return result;
        }
        #endregion
    }
}

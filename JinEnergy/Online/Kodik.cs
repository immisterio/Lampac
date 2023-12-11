using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using Shared.Model.Online.Kodik;

namespace JinEnergy.Online
{
    public class KodikController : BaseController
    {
        #region KodikInvoke
        static bool origstream;

        static KodikInvoke oninvk = new KodikInvoke
        (
            null,
            AppInit.Kodik.apihost!,
            AppInit.Kodik.token,
            (uri, head) => JsHttpClient.Get(AppInit.Kodik.corsHost(uri), addHeaders: head),
            (uri, data) => JsHttpClient.Post(AppInit.Kodik.corsHost(uri), data),
            streamfile => HostStreamProxy(streamfile, origstream)
            //AppInit.log
        );
        #endregion

        [JSInvokable("lite/kodik")]
        async public static ValueTask<string> Index(string args)
        {
            var arg = defaultArgs(args);
            string? kid = parse_arg("kid", args);
            string? pick = parse_arg("pick", args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");

            List<Result>? content = null;

            if (arg.clarification == 1 || (arg.kinopoisk_id == 0 && string.IsNullOrWhiteSpace(arg.imdb_id)))
            {
                if (string.IsNullOrWhiteSpace(arg.title))
                    return EmptyError("arg");

                var res = await InvokeCache(arg.id, $"kodik:search:{arg.title}", () => oninvk.Embed(arg.title));
                if (res?.result == null || res.result.Count == 0)
                    return EmptyError("content");

                if (string.IsNullOrEmpty(pick))
                    return res.html ?? string.Empty;

                content = oninvk.Embed(res.result, pick);
            }
            else
            {
                if (arg.kinopoisk_id == 0 && string.IsNullOrWhiteSpace(arg.imdb_id))
                    return EmptyError("arg");

                content = await InvokeCache(arg.id, $"kodik:search:{arg.kinopoisk_id}:{arg.imdb_id}", () => oninvk.Embed(arg.imdb_id, arg.kinopoisk_id, s));
                if (content == null || content.Count == 0)
                    return EmptyError("content");
            }

            return oninvk.Html(content, arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, arg.clarification, pick, kid, s, false);
        }


        #region VideoParse
        [JSInvokable("lite/kodik/video")]
        async public static ValueTask<string> VideoParse(string args)
        {
            var arg = defaultArgs(args);
            int episode = int.Parse(parse_arg("episode", args) ?? "0");
            string? link = parse_arg("link", args);
            if (link == null)
                return EmptyError("link");

            var streams = await InvokeCache(0, $"kodik:video:{link}", () => oninvk.VideoParse(AppInit.Kodik.linkhost, link));
            if (streams == null)
                return EmptyError("streams");

            origstream = await IsOrigStream(streams[0].url);

            string? result = oninvk.VideoParse(streams, arg.title, arg.original_title, episode, false);
            if (result == null)
                return EmptyError("result");

            return result;
        }
        #endregion
    }
}

using JinEnergy.Engine;
using Lampac.Models.LITE;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using Shared.Model.Online.Kodik;

namespace JinEnergy.Online
{
    public class KodikController : BaseController
    {
        #region KodikInvoke
        static bool origstream;

        static KodikInvoke kodikInvoke(string args, KodikSettings init)
        {
            bool userapn = IsApnIncluded(init);

            return new KodikInvoke
            (
                null,
                init.apihost!,
                init.token,
                init.hls,
                (uri, head) => JsHttpClient.Get(init.cors(uri), httpHeaders(args, init, head)),
                (uri, data) => JsHttpClient.Post(init.cors(uri), data, httpHeaders(args, init)),
                streamfile => userapn ? HostStreamProxy(init, streamfile) : DefaultStreamProxy(streamfile, origstream)
                //AppInit.log
            );
        }
        #endregion

        [JSInvokable("lite/kodik")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Kodik;
            var oninvk = kodikInvoke(args, init);

            var arg = defaultArgs(args);
            string? kid = parse_arg("kid", args);
            string? pick = parse_arg("pick", args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");

            List<Result>? content = null;

            if (arg.clarification == 1)
            {
                if (string.IsNullOrEmpty(arg.title))
                    return EmptyError("title");

                var res = await InvokeCache(arg.id, $"kodik:search:{arg.title}", () => oninvk.Embed(arg.title));
                if (res?.result == null || res.result.Count == 0)
                    return EmptyError("content");

                if (string.IsNullOrEmpty(pick))
                    return res.html ?? string.Empty;

                content = oninvk.Embed(res.result, pick);
            }
            else
            {
                if (arg.kinopoisk_id == 0 && string.IsNullOrEmpty(arg.imdb_id))
                    return EmptyError("kinopoisk_id / imdb_id");

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
            var init = AppInit.Kodik.Clone();
            var oninvk = kodikInvoke(args, init);

            var arg = defaultArgs(args);
            int episode = int.Parse(parse_arg("episode", args) ?? "0");
            string? link = parse_arg("link", args);
            if (link == null)
                return EmptyError("link");

            string memkey = $"kodik:video:{link}";
            refresh: var streams = await InvokeCache(0, memkey, () => oninvk.VideoParse(init.linkhost, link));

            if (streams != null && !IsApnIncluded(init))
                origstream = await IsOrigStream(streams[0].url);

            string result = oninvk.VideoParse(streams, arg.title, arg.original_title, episode, false);
            if (string.IsNullOrEmpty(result))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init))
                    goto refresh;
            }

            return result;
        }
        #endregion
    }
}

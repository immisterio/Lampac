using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class VideoCDNController : BaseController
    {
        #region VideoCDNController
        static bool origstream;

        static string? lastcheckid;
        #endregion

        [JSInvokable("lite/vcdn")]
        async public static ValueTask<string> Index(string args)
        {
            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            string? t = parse_arg("t", args);

            var oninvk = new VideoCDNInvoke
            (
               null,
               AppInit.VCDN.corsHost(),
               AppInit.VCDN.corsHost(AppInit.VCDN.apihost!),
               AppInit.VCDN.token!,
               (url, referer) => JsHttpClient.Get(AppInit.VCDN.corsHost(url), addHeaders: new List<(string name, string val)> { ("referer", referer) }),
               streamfile => HostStreamProxy(streamfile, origstream)
               //AppInit.log
            );

            if (arg.kinopoisk_id == 0 && string.IsNullOrWhiteSpace(arg.imdb_id))
            {
                string? similars = await InvokeCache(arg.id, $"videocdn:search:{arg.title}:{arg.original_title}", () => oninvk.Search(arg.title!, arg.original_title));
                if (similars == null)
                    return EmptyError("similars");

                return similars;
            }

            var content = await InvokeCache(arg.id, $"videocdn:view:{arg.imdb_id}:{arg.kinopoisk_id}", () => oninvk.Embed(arg.kinopoisk_id, arg.imdb_id));
            if (content == null)
                return EmptyError("content");

            string checkid = $"{arg.imdb_id}:{arg.kinopoisk_id}";
            if (AppInit.Country == "UA" && lastcheckid != checkid)
            {
                string? uri = oninvk.FirstLink(content, t, s);
                if (!string.IsNullOrEmpty(uri))
                {
                    lastcheckid = checkid;
                    origstream = await IsOrigStream(uri);
                }
            }

            return oninvk.Html(content, arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, t, s);
        }
    }
}

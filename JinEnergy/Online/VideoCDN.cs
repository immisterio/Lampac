using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class VideoCDNController : BaseController
    {
        [JSInvokable("lite/vcdn")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.VCDN.Clone();
            bool userapn = IsApnIncluded(init);

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int serial = int.Parse(parse_arg("serial", args) ?? "-1");
            string? t = parse_arg("t", args);

            var oninvk = new VideoCDNInvoke
            (
               null,
               init.corsHost(),
               init.cors(init.apihost!),
               init.token!,
               init.hls,
               (url, referer) => JsHttpClient.Get(init.cors(url), addHeaders: new List<(string name, string val)> { ("referer", referer) }),
               streamfile => userapn ? HostStreamProxy(init, streamfile) : DefaultStreamProxy(streamfile)
               //AppInit.log
            );

            #region similars
            if (arg.kinopoisk_id == 0 && string.IsNullOrWhiteSpace(arg.imdb_id))
            {
                string similar_memkey = $"videocdn:search:{arg.title}:{arg.original_title}";
                similar_refresh: string? similars = await InvokeCache(arg.id, similar_memkey, () => oninvk.Search(arg.title!, arg.original_title, serial));

                if (string.IsNullOrEmpty(similars))
                {
                    IMemoryCache.Remove(similar_memkey);
                    if (IsRefresh(init))
                        goto similar_refresh;

                    return EmptyError("similars");
                }

                return similars;
            }
            #endregion

            string memkey = $"videocdn:view:{arg.imdb_id}:{arg.kinopoisk_id}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.kinopoisk_id, arg.imdb_id));

            string html = oninvk.Html(content, arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, t, s);
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

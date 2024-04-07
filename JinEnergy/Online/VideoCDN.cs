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

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int serial = int.Parse(parse_arg("serial", args) ?? "-1");
            string? t = parse_arg("t", args);

            var oninvk = new VideoCDNInvoke
            (
               init,
               (url, referer) => JsHttpClient.Get(init.cors(url), androidHttpReq: false, addHeaders: httpHeaders(args, init)),
               streamfile => HostStreamProxy(init, streamfile)
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
                    if (IsRefresh(init, true))
                        goto similar_refresh;

                    return EmptyError("similars");
                }

                return similars;
            }
            #endregion

            string memkey = $"videocdn:view:{arg.imdb_id}:{arg.kinopoisk_id}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => 
            {
                AppInit.JSRuntime?.InvokeAsync<object>("eval", "$('head meta[name=\"referrer\"]').attr('content', 'origin');");
                var res = oninvk.Embed(arg.kinopoisk_id, arg.imdb_id);
                AppInit.JSRuntime?.InvokeAsync<object>("eval", "$('head meta[name=\"referrer\"]').attr('content', 'no-referrer');");
                return res;

            });

            string html = oninvk.Html(content, arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, t, s);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init, true))
                    goto refresh;
            }

            return html;
        }
    }
}

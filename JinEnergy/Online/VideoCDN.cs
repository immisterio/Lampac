using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class VideoCDNController : BaseController
    {
        [JSInvokable("lite/vcdn")]
        async public static Task<string> Index(string args)
        {
            int s = int.Parse(arg("s", args) ?? "-1");
            string? t = arg("t", args);
            defaultOnlineArgs(args, out long id, out string? imdb_id, out long kinopoisk_id, out string? title, out string? original_title, out int serial, out string? original_language, out int year, out string? source, out int clarification, out long cub_id, out string? account_email);

            var oninvk = new VideoCDNInvoke
            (
               null,
               AppInit.VCDN.corsHost(),
               (url, referer) => JsHttpClient.Get(AppInit.VCDN.corsHost(url), addHeaders: new List<(string name, string val)> { ("referer", referer) }),
               streamfile => streamfile
               //AppInit.log
            );

            var content = await InvokeCache(id, $"videocdn:view:{imdb_id}:{kinopoisk_id}", () => 
            {
                if (!AppInit.IsAndrod)
                    AppInit.JSRuntime?.InvokeAsync<object>("eval", "$('head meta[name=\"referrer\"]').attr('content', 'origin');");

                var res = oninvk.Embed(kinopoisk_id, imdb_id);

                if (!AppInit.IsAndrod)
                    AppInit.JSRuntime?.InvokeAsync<object>("eval", "$('head meta[name=\"referrer\"]').attr('content', 'no-referrer');");

                return res;
            });

            if (content == null)
                return OnError(string.Empty);

            return oninvk.Html(content, imdb_id, kinopoisk_id, title, original_title, t, s);
        }
    }
}

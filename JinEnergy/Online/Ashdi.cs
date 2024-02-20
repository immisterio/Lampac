using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class AshdiController : BaseController
    {
        [JSInvokable("lite/ashdi")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Ashdi.Clone();

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int t = int.Parse(parse_arg("t", args) ?? "-1");

            if (arg.kinopoisk_id == 0)
                return EmptyError("kinopoisk_id");

            var oninvk = new AshdiInvoke
            (
               null,
               init.corsHost(),
               ongettourl => JsHttpClient.Get(init.cors(ongettourl), httpHeaders(args, init)),
               streamfile => HostStreamProxy(init, streamfile)
            );

            string memkey = $"ashdi:view:{arg.kinopoisk_id}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.kinopoisk_id));

            string html = oninvk.Html(content, arg.kinopoisk_id, arg.title, arg.original_title, t, s);
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

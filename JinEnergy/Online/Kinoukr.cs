using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class KinoukrController : BaseController
    {
        [JSInvokable("lite/kinoukr")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Kinoukr.Clone();

            var arg = defaultArgs(args);
            string? href = parse_arg("href", args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int t = int.Parse(parse_arg("t", args) ?? "-1");
            
            var oninvk = new KinoukrInvoke
            (
               null,
               init.corsHost(),
               ongettourl => JsHttpClient.Get(init.cors(ongettourl), httpHeaders(args, init)),
               (url, data) => JsHttpClient.Post(init.cors(url), data, httpHeaders(args, init)),
               streamfile => HostStreamProxy(init, streamfile)
               //AppInit.log
            );

            string memkey = string.IsNullOrEmpty(href) ? $"kinoukr:{arg.original_title}:{arg.year}:{arg.clarification}" : $"kinoukr:{href}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.clarification == 1 ? arg.title : arg.original_title, arg.year, href));

            string html = oninvk.Html(content, arg.clarification, arg.title, arg.original_title, arg.year, t, s, href);
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

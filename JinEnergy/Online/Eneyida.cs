using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class EneyidaController : BaseController
    {
        [JSInvokable("lite/eneyida")]
        async public static ValueTask<string> Index(string args)
        {
            var arg = defaultArgs(args);
            string? href = parse_arg("href", args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int t = int.Parse(parse_arg("t", args) ?? "-1");
            
            var oninvk = new EneyidaInvoke
            (
               null,
               AppInit.Eneyida.corsHost(),
               ongettourl => JsHttpClient.Get(AppInit.Eneyida.corsHost(ongettourl)),
               (url, data) => JsHttpClient.Post(AppInit.Eneyida.corsHost(url), data),
               onstreamtofile => onstreamtofile
               //AppInit.log
            );

            var content = await InvokeCache(arg.id, $"eneyida:view:{arg.original_title}:{arg.year}:{href}:{arg.clarification}", () => oninvk.Embed(arg.clarification == 1 ? arg.title : arg.original_title, arg.year, href));
            if (content == null)
                return OnError("content");

            return oninvk.Html(content, arg.clarification, arg.title, arg.original_title, arg.year, t, s, href);
        }
    }
}

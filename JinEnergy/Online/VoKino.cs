using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class VoKinoController : BaseController
    {
        [JSInvokable("lite/vokino")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.VoKino.Clone();
            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");

            var oninvk = new VoKinoInvoke
            (
               null,
               init.corsHost(),
               init.token!,
               ongettourl => JsHttpClient.Get(init.cors(ongettourl), httpHeaders(args, init)),
               streamfile => HostStreamProxy(init, streamfile)
            );

            refresh: var content = await oninvk.Embed(arg.kinopoisk_id);

            string html = oninvk.Html(content, arg.kinopoisk_id, arg.title, arg.original_title, s);
            if (string.IsNullOrEmpty(html) && IsRefresh(init, true))
                goto refresh;

            return html;
        }
    }
}

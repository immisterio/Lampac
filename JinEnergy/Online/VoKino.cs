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

            var oninvk = new VoKinoInvoke
            (
               null,
               init.corsHost(),
               init.token!,
               ongettourl => JsHttpClient.Get(init.cors(ongettourl), httpHeaders(args, init)),
               streamfile => HostStreamProxy(init, streamfile)
            );

            refresh: var content = await oninvk.Embed(arg.kinopoisk_id);

            string html = oninvk.Html(content, arg.title, arg.original_title);
            if (string.IsNullOrEmpty(html) && IsRefresh(init, true))
                goto refresh;

            return html;
        }
    }
}

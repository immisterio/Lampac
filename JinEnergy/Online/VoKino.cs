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
            var init = AppInit.VoKino;
            var arg = defaultArgs(args);

            var oninvk = new VoKinoInvoke
            (
               null,
               init.corsHost(),
               init.token,
               ongettourl => JsHttpClient.Get(init.cors(ongettourl)),
               onstreamtofile => onstreamtofile
            );

            var content = await InvokeCache(arg.id, $"vokino:view:{arg.kinopoisk_id}", () => oninvk.Embed(arg.kinopoisk_id));
            if (content == null)
                return EmptyError("content");

            return oninvk.Html(content, arg.title, arg.original_title);
        }
    }
}

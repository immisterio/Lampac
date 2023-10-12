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
            var arg = defaultArgs(args);

            if (arg.kinopoisk_id == 0 || !AppInit.VoKino.enable || string.IsNullOrEmpty(AppInit.VoKino.token))
                return EmptyError("kinopoisk_id");

            var oninvk = new VoKinoInvoke
            (
               null,
               AppInit.VoKino.corsHost(),
               AppInit.VoKino.token,
               ongettourl => JsHttpClient.Get(AppInit.VoKino.corsHost(ongettourl)),
               onstreamtofile => onstreamtofile
            );

            var content = await InvokeCache(arg.id, $"vokino:view:{arg.kinopoisk_id}", () => oninvk.Embed(arg.kinopoisk_id));
            if (content == null)
                return EmptyError("content");

            return oninvk.Html(content, arg.title, arg.original_title);
        }
    }
}

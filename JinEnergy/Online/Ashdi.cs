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
            var init = AppInit.Ashdi;

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int t = int.Parse(parse_arg("t", args) ?? "-1");

            if (arg.kinopoisk_id == 0)
                return EmptyError("kinopoisk_id");

            var oninvk = new AshdiInvoke
            (
               null,
               init.corsHost(),
               ongettourl => JsHttpClient.Get(init.cors(ongettourl)),
               onstreamtofile => onstreamtofile
            );

            var content = await InvokeCache(arg.id, $"ashdi:view:{arg.kinopoisk_id}", () => oninvk.Embed(arg.kinopoisk_id));

            if (content == null)
                return EmptyError("content");

            if (string.IsNullOrEmpty(content.content) && content.serial == null)
                return EmptyError("content 2");

            return oninvk.Html(content, arg.kinopoisk_id, arg.title, arg.original_title, t, s);
        }
    }
}

using JinEnergy.Engine;
using Lampac.Models.LITE.AniLibria;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class AniLibriaController : BaseController
    {
        [JSInvokable("lite/anilibria")]
        async public static Task<string> Index(string args)
        {
            var arg = defaultArgs(args);
            string? code = parse_arg("code", args);

            if (string.IsNullOrEmpty(arg.title))
                return OnError("arg");
            
            var oninvk = new AniLibriaInvoke
            (
               null,
               AppInit.AnilibriaOnline.corsHost(),
               ongettourl => JsHttpClient.Get<List<RootObject>>(AppInit.AnilibriaOnline.corsHost(ongettourl)),
               onstreamtofile => onstreamtofile
               //AppInit.log
            );

            var result = await InvokeCache(arg.id, $"anilibriaonline:{arg.title}", () => oninvk.Embed(arg.title));
            if (result == null)
                return OnError("result");

            return oninvk.Html(result, arg.title, code, arg.year);
        }
    }
}

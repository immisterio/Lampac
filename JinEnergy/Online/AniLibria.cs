using JinEnergy.Engine;
using Lampac.Models.LITE.AniLibria;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class AniLibriaController : BaseController
    {
        [JSInvokable("lite/anilibria")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.AnilibriaOnline.Clone();

            var arg = defaultArgs(args);
            string? code = parse_arg("code", args);

            if (string.IsNullOrEmpty(arg.title))
                return EmptyError("arg");
            
            var oninvk = new AniLibriaInvoke
            (
               null,
               init.corsHost(),
               ongettourl => JsHttpClient.Get<List<RootObject>>(init.cors(ongettourl), httpHeaders(args, init)),
               streamfile => HostStreamProxy(init, streamfile)
               //AppInit.log
            );

            string memkey = $"anilibriaonline:{arg.title}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.title));

            string html = oninvk.Html(content, arg.title, code, arg.year);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init))
                    goto refresh;
            }

            return html;
        }
    }
}

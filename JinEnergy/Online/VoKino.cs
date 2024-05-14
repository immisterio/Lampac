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
            string? balancer = parse_arg("balancer", args);
            string? t = parse_arg("t", args);

            if (balancer is "filmix" or "zetflix" or "ashdi" or "rhs" or "collaps")
                init.streamproxy = false;

            var oninvk = new VoKinoInvoke
            (
               null,
               init.corsHost(),
               init.token!,
               ongettourl => JsHttpClient.Get(init.cors(ongettourl), httpHeaders(args, init)),
               streamfile => HostStreamProxy(init, streamfile)
            );

            string memkey = $"vokino:{arg.kinopoisk_id}:{balancer}:{t}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.kinopoisk_id, balancer, t));

            string html = oninvk.Html(content, arg.kinopoisk_id, arg.title, arg.original_title, balancer, t, s);
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

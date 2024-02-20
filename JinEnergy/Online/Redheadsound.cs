using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class RedheadsoundController : BaseController
    {
        [JSInvokable("lite/redheadsound")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Redheadsound.Clone();

            var arg = defaultArgs(args);
            int clarification = arg.clarification;

            if (arg.original_language != "en")
                clarification = 1;

            if (string.IsNullOrWhiteSpace(arg.title) || arg.year == 0)
                return EmptyError("title");

            var oninvk = new RedheadsoundInvoke
            (
               null,
               init.corsHost(),
               ongettourl => JsHttpClient.Get(init.cors(ongettourl), httpHeaders(args, init)),
               (url, data) => JsHttpClient.Post(init.cors(url), data, httpHeaders(args, init)),
               streamfile => HostStreamProxy(init, streamfile)
            );

            refresh: var content = await oninvk.Embed(clarification == 1 ? arg.title : (arg.original_title ?? arg.title), arg.year);

            string html = oninvk.Html(content, arg.title);
            if (string.IsNullOrEmpty(html) && IsRefresh(init))
                goto refresh;

            return html;
        }
    }
}

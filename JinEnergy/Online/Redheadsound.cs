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
            var init = AppInit.Redheadsound;

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
               ongettourl => JsHttpClient.Get(init.cors(ongettourl)),
               (url, data) => JsHttpClient.Post(init.cors(url), data),
               streamfile => streamfile
            );

            var content = await InvokeCache(arg.id, $"redheadsound:view:{arg.title}:{arg.year}:{clarification}", () => oninvk.Embed(clarification == 1 ? arg.title : (arg.original_title ?? arg.title), arg.year));
            if (content == null)
                return EmptyError("content");

            return oninvk.Html(content, arg.title);
        }
    }
}

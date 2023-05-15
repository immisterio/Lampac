using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class RedheadsoundController : BaseController
    {
        [JSInvokable("lite/redheadsound")]
        async public static Task<string> Index(string args)
        {
            var arg = defaultArgs(args);
            int clarification = arg.clarification;

            if (arg.original_language != "en")
                clarification = 1;

            if (string.IsNullOrWhiteSpace(arg.title) || arg.year == 0)
                return OnError("title");

            var oninvk = new RedheadsoundInvoke
            (
               null,
               AppInit.Redheadsound.corsHost(),
               ongettourl => JsHttpClient.Get(AppInit.Redheadsound.corsHost(ongettourl)),
               (url, data) => JsHttpClient.Post(AppInit.Redheadsound.corsHost(url), data),
               streamfile => streamfile
            );

            var content = await InvokeCache(arg.id, $"redheadsound:view:{arg.title}:{arg.year}:{clarification}", () => oninvk.Embed(clarification == 1 ? arg.title : (arg.original_title ?? arg.title), arg.year));
            if (content == null)
                return OnError("content");

            return oninvk.Html(content, arg.title);
        }
    }
}

using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class iRemux : BaseController
    {
        #region iRemuxInvoke
        static iRemuxInvoke oninvk = new iRemuxInvoke
        (
            null,
            AppInit.Voidboost.corsHost(),
            ongettourl => JsHttpClient.Get(AppInit.iRemux.cors(ongettourl)),
            (url, data) => JsHttpClient.Post(AppInit.iRemux.cors(url), data),
            streamfile => HostStreamProxy(AppInit.iRemux, streamfile)
        );
        #endregion

        [JSInvokable("lite/remux")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.iRemux.Clone();

            var arg = defaultArgs(args);

            if (string.IsNullOrWhiteSpace(arg.title ?? arg.original_title) || arg.year == 0)
                return EmptyError("title");

            string? content = await InvokeCache(arg.id, $"remux:{arg.title}:{arg.original_title}:{arg.year}", () => oninvk.Embed(arg.title, arg.original_title, arg.year));
            if (string.IsNullOrEmpty(content))
                return EmptyError("content");

            return content;
        }

        [JSInvokable("lite/remux/movie")]
        async public static ValueTask<string> Movie(string args)
        {
            if (!AppInit.iRemux.enable)
                return EmptyError("enable");

            var arg = defaultArgs(args);
            string? id = parse_arg("id", args);

            string? weblink = await InvokeCache(0, $"remux:view:{id}", () => oninvk.Weblink(id));
            if (weblink == null)
                return EmptyError("weblink");

            return oninvk.Movie(weblink, arg.title, arg.original_title);
        }
    }
}

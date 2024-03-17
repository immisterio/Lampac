using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using Shared.Model.Base;

namespace JinEnergy.Online
{
    public class iRemux : BaseController
    {
        #region iRemuxInvoke
        static iRemuxInvoke remuxInvoke(string args, BaseSettings init)
        {
            return new iRemuxInvoke
            (
                null,
                AppInit.iRemux.corsHost(),
                ongettourl => JsHttpClient.Get(AppInit.iRemux.cors(ongettourl), httpHeaders(args, init)),
                (url, data) => JsHttpClient.Post(AppInit.iRemux.cors(url), data, httpHeaders(args, init)),
                streamfile => HostStreamProxy(AppInit.iRemux, streamfile)
            );
        }
        #endregion

        [JSInvokable("lite/remux")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.iRemux.Clone();

            var arg = defaultArgs(args);
            string? href = parse_arg("href", args);

            if (string.IsNullOrWhiteSpace(arg.title ?? arg.original_title) || arg.year == 0)
                return EmptyError("title");

            var oninvk = remuxInvoke(args, init);

            var content = await InvokeCache(arg.id, $"remux:{arg.title}:{arg.original_title}:{arg.year}:{href}", () => oninvk.Embed(arg.title, arg.original_title, arg.year, href));
            if (content == null)
                return EmptyError("content");

            return oninvk.Html(content, arg.title, arg.original_title, arg.year);
        }


        [JSInvokable("lite/remux/movie")]
        async public static ValueTask<string> Movie(string args)
        {
            if (!AppInit.iRemux.enable)
                return EmptyError("enable");

            var arg = defaultArgs(args);
            string? linkid = parse_arg("linkid", args);

            var oninvk = remuxInvoke(args, AppInit.iRemux);

            string? weblink = await InvokeCache(0, $"remux:view:{linkid}", () => oninvk.Weblink(linkid));
            if (weblink == null)
                return EmptyError("weblink");

            return oninvk.Movie(weblink, parse_arg("quality", args), arg.title, arg.original_title);
        }
    }
}
